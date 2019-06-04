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
        public async Task Build(Graph graph, Solution solution, Project project, Document doc)
        {
            var model = doc.GetSemanticModelAsync().Result;

            var members = GetExplicitlyDeclaredMembersOfAllClassesInModel(model);

            var referencesBetwenMembers = GetMemberReferences(solution, model, doc, members);

            referencesBetwenMembers = AbsorbGettersSettersIntoProp(referencesBetwenMembers);

            referencesBetwenMembers = referencesBetwenMembers.Distinct().ToArray();

            foreach (var reference in referencesBetwenMembers)
            {
                graph.AddEdge(  source: reference.RefSymbol.Name, 
                                target: reference.OriginalSymbol.Name);

                graph.FindNode(reference.RefSymbol.Name).UserData = reference.RefSymbol;
                graph.FindNode(reference.OriginalSymbol.Name).UserData = reference.OriginalSymbol;
            }

            var survivedMembers = referencesBetwenMembers
                                            .SelectMany(x => new[] { x.RefSymbol, x.OriginalSymbol })
                                            .Distinct()
                                            .ToArray();

            foreach (var field in survivedMembers.OfType<IFieldSymbol>())
            {
                var node = graph.FindNode(field.Name);
                //node.LabelText = GetText(field);

                node.Attr.FillColor = Color.Orange;
            }
 
            foreach (var prop in survivedMembers.OfType<IPropertySymbol>())
            {
                var node = graph.FindNode(prop.Name);
                //node.LabelText = GetText(prop);

                node.Attr.FillColor = Color.LightPink;
            }

            foreach (var method in survivedMembers.OfType<IMethodSymbol>())
            {
                var node = graph.FindNode(method.Name);
                node.Attr.FillColor = Color.LightBlue;
                //node.LabelText = GetText(method);
            }

            foreach (var node in graph.Nodes)
            {
                if(node.UserData is IPropertySymbol
                   && node.LabelText.Contains(" set;"))
                {
                    node.Attr.FillColor = Color.Orange;
                }
            }

            graph.Nodes
                .Where(node => (node.UserData as ISymbol)?.DeclaredAccessibility == Accessibility.Public)
                .ForEach(node =>
                {
                    node.Attr.FillColor = Color.LightGreen;
                });

            //no externals for now, so this will do nothing
            referencesBetwenMembers
                        .Where(x => x.RefDocument != doc)
                        .ForEach(x =>
                        {
                            var node = graph.FindNode(x.RefSymbol.Name);
                            node.LabelText = $"{x.RefDocument.Name}\r\n{node.LabelText}";
                            node.Attr.FillColor = Color.Red;
                        });

            graph.Nodes.ForEach(node =>
            {
                node.Attr.Padding = 1;
                node.LabelText = GetText(node.UserData as ISymbol);
                node.UserData = null;
            });
        }

        private SymbolRef[] AbsorbGettersSettersIntoProp(SymbolRef[] referencesBetwenMembers)
        {
            var gettersSetters = referencesBetwenMembers
                                            .Where(x => x.OriginalSymbol is IMethodSymbol
                                                        && x.RefSymbol is IPropertySymbol);

            var referencesToGettersSetters = referencesBetwenMembers
                                                    .Where(x => gettersSetters.Any(y => y.OriginalSymbol == x.RefSymbol));

            var newReferences = referencesToGettersSetters
                                    .SelectMany(@ref => 
                                    {
                                        var targets = gettersSetters.Where(x => x.OriginalSymbol == @ref.RefSymbol);

                                        return targets.Select(t => new SymbolRef(
                                                                        @ref.OriginalSymbol, 
                                                                        t.RefDocument, 
                                                                        t.RefLocation, 
                                                                        t.RefSymbol));
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
                                        .Where(x => x.Document == doc)
                                        .Select(x => new SymbolRef(member, x.Document, x.Location));

                        references.AddRange(refs);
                        break;
                }
            }

            references.AsParallel().ForEach(async (x) =>
            {
                var locRoot = await x.RefDocument.GetSyntaxRootAsync();
                var node = locRoot.FindNode(x.RefLocation.SourceSpan);

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
                x.RefSymbol = referencingSymbol;
            });

            return references.Where(x => x.RefSymbol != null).ToArray();
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

        private static string GetText(ISymbol symb)
        {
            SyntaxNode node = symb.DeclaringSyntaxReferences.First()
                                    .GetSyntax();

            if(symb is IFieldSymbol)
            {
                node = node.FirstAncestorOrSelf<FieldDeclarationSyntax>();
            }
                               
            var text = new StringBuilder(node.ToFullString());

            var spacesToTrim = text.ToString()
                                     .Split(new[] { "\r\n" }, StringSplitOptions.None)
                                     .Where(x => string.IsNullOrWhiteSpace(x) == false)
                                     .Select(line => line.Length - line.TrimStart().Length)
                                     .Min();

            if(spacesToTrim != 0)
            {
                var spaces = "\r\n" + new string(' ', spacesToTrim);
                text.Replace(spaces, "\r\n ");
            }
         
            return " " + text.ToString().TrimStart();
        }
    }
}
