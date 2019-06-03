using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.Msagl.Drawing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticTreeGraph
{
    public class GraphBuilder
    {
        public async Task Build(Graph graph, Solution solution)
        {
            var project = solution.Projects.ElementAt(0);
            var doc = project.Documents.ElementAt(0);
            var model = doc.GetSemanticModelAsync().Result;

            var members = GetExplicitlyDeclaredMembersOfAllClassesInModel(model);

            var referencesBetwenMembers = GetMemberReferences(solution, model, doc, members);

            referencesBetwenMembers = AbsorbGettersSettersIntoProp(referencesBetwenMembers);

            foreach (var reference in referencesBetwenMembers)
            {
                graph.AddEdge(  source: reference.ReferencingSymbol.Name, 
                                target: reference.OriginalSymbol.Name);

                graph.FindNode(reference.ReferencingSymbol.Name).UserData = reference.ReferencingSymbol;
                graph.FindNode(reference.OriginalSymbol.Name).UserData = reference.OriginalSymbol;
            }

            foreach (var field in members.OfType<IFieldSymbol>())
            {
                var stateNode = graph.FindNode(field.Name);
           
                stateNode.Attr.FillColor = Color.LightBlue;
            }
 
            foreach (var prop in members.OfType<IPropertySymbol>())
            {
                var stateNode = graph.FindNode(prop.Name);

                stateNode.Attr.FillColor = Color.LightGreen;
            }
        }

        private SymbolRef[] AbsorbGettersSettersIntoProp(SymbolRef[] referencesBetwenMembers)
        {
            var gettersSetters = referencesBetwenMembers
                                            .Where(x => x.OriginalSymbol is IMethodSymbol
                                                        && x.ReferencingSymbol is IPropertySymbol);

            var referencesToGettersSetters = referencesBetwenMembers
                                                    .Where(x => gettersSetters.Any(y => y.OriginalSymbol == x.ReferencingSymbol));

            var newReferences = referencesToGettersSetters
                                    .SelectMany(@ref => 
                                    {
                                        var targets = gettersSetters.Where(x => x.OriginalSymbol == @ref.ReferencingSymbol);

                                        return targets.Select(t => new SymbolRef(
                                                                        @ref.OriginalSymbol, 
                                                                        t.Document, 
                                                                        t.Location, 
                                                                        t.ReferencingSymbol));
                                    });


            var refsWithoutGetterSetters = referencesBetwenMembers
                                                    .Except(gettersSetters)
                                                    .Except(referencesToGettersSetters)
                                                    .Union(newReferences);

            return refsWithoutGetterSetters.ToArray();
        }

        private static SymbolRef[] GetMemberReferences(Solution solution, SemanticModel model, Document doc, ISymbol[] members)
        {
            var references = new List<SymbolRef>();

            foreach (var member in members)
            {
                switch (member)
                {
                    // point property accessors to declaring property
                    case IMethodSymbol m when m.AssociatedSymbol != null:

                        m.AssociatedSymbol
                                        .Locations
                                        .Select(x => new SymbolRef(m, doc, x))
                                        .Pipe(references.AddRange);
                        break;

                    // point backing fileds to declaring property
                    case IFieldSymbol f when f.AssociatedSymbol != null:

                        f.AssociatedSymbol
                                    .Locations
                                    .Select(x => new SymbolRef(f, doc, x))
                                    .Pipe(references.AddRange);
                        break;

                    default:

                        var refs = SymbolFinder
                                        .FindReferencesAsync(member, solution)
                                        .Result
                                        .SelectMany(x => x.Locations)
                                        .Select(x => new SymbolRef(member, x.Document, x.Location));

                        references.AddRange(refs);
                        break;
                }
            }

            references.AsParallel().ForEach(async (x) =>
            {
                var locRoot = await x.Document.GetSyntaxRootAsync();
                var node = locRoot.FindNode(x.Location.SourceSpan);

                var lookForMembers = new[]
                {
                        SyntaxKind.GetAccessorDeclaration,
                        SyntaxKind.MethodDeclaration,
                        SyntaxKind.PropertyDeclaration,
                        SyntaxKind.SetAccessorDeclaration
                    };

                var methodNode = node
                                    .AncestorsAndSelf()
                                    .Where(y => lookForMembers.Contains(y.Kind()))
                                    .FirstOrDefault();

                if (methodNode == null)
                {
                    return;
                }

                var referencingSymbol = model.GetDeclaredSymbol(methodNode);
                x.ReferencingSymbol = referencingSymbol;
            });

            return references.Where(x => x.ReferencingSymbol != null).ToArray();
        }

        private static ISymbol[] GetExplicitlyDeclaredMembersOfAllClassesInModel(SemanticModel model)
        {
            var syntaxRoot = model.SyntaxTree.GetRoot();

            var classNodes = syntaxRoot
                                .DescendantNodesAndSelf()
                                .Where(x => x.Kind() == SyntaxKind.ClassDeclaration); // todo add structs?

            var classSymbols = classNodes.Select(x => model.GetDeclaredSymbol(x) as INamedTypeSymbol);

            var members = classSymbols
                                .SelectMany(x => x.GetMembers())
                                .Where(x => x.IsImplicitlyDeclared == false);

            return members.ToArray();
        }
    }
}
