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
        public void Build(Graph graph, Solution solution)
        {

            var project = solution.Projects.ElementAt(0);
            var doc = project.Documents.ElementAt(0);
            var model = doc.GetSemanticModelAsync().Result;

            var root = model.SyntaxTree.GetRoot();

            var classNode = root
                                .DescendantNodesAndSelf()
                                .Where(x => x.Kind() == SyntaxKind.ClassDeclaration); // todo add structs

            var classSymbols = classNode.Select(x => model.GetDeclaredSymbol(x) as INamedTypeSymbol);

            var members = classSymbols
                                .SelectMany(x => x.GetMembers())
                                .Where(x => x.IsImplicitlyDeclared == false);

            var edgeLocation2 = new List<SymbolRef>();

            foreach (var member in members)
            {
                switch (member)
                {
                    case IMethodSymbol m when m.AssociatedSymbol != null:
                        edgeLocation2.Add(new SymbolRef(m, doc, m.AssociatedSymbol.Locations.ElementAt(0)));
                        break;
                    default:
                        var refs = SymbolFinder
                                        .FindReferencesAsync(member, solution)
                                        .Result
                                        .SelectMany(x => x.Locations)
                                        .Select(x => new SymbolRef(member, x.Document, x.Location));
                        edgeLocation2.AddRange(refs);
                        break;
                }
            }

            var edgeSymbols = edgeLocation2.AsParallel().Select(async (x) =>
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
                    return null;
                }

                var symbol = model.GetDeclaredSymbol(methodNode);
                return new { member = x.Symbol, methodNode, symbol };
            })
            .Where(x => x != null);

            foreach (var edgeTask in edgeSymbols)
            {
                var edge = edgeTask.Result;
                graph.AddEdge(edge.symbol.Name, edge.member.Name);
                var codeNode = graph.FindNode(edge.symbol.Name);

                var text = edge.methodNode.GetText().ToString().Replace("\r\n       ", "\r\n");

                if (text.StartsWith("\r\n"))
                {
                    text = text.Substring(2);
                }

                text = text.Trim();

                if (text.StartsWith("get\r\n"))
                {
                    codeNode.LabelText = "get";
                }

                else if (text.StartsWith("set\r\n"))
                {
                    codeNode.LabelText = "set";
                }
                else
                {
                    codeNode.LabelText = text;
                }
            }

            foreach (var field in members.OfType<IFieldSymbol>())
            {
                var stateNode = graph.FindNode(field.Name);

                string text;

                if (field.DeclaringSyntaxReferences.Any() == false)
                {
                    text = field.ToDisplayString();
                }
                else
                {
                    text = field.DeclaringSyntaxReferences
                                .Single()
                                .GetSyntax()
                                .FirstAncestorOrSelf<FieldDeclarationSyntax>()
                                .GetText().ToString()
                                .Replace("       ", "");
                }

                if (text.StartsWith("\r\n"))
                {
                    text = text.Substring(2);
                }

                stateNode.LabelText = text;
                stateNode.Attr.FillColor = Color.LightBlue;
            }

            var propertyGets = graph.Nodes.Where(x => x.LabelText.StartsWith("get_")
                                                && x.OutEdges.Any() == false);

            foreach (var node in propertyGets)
            {
                node.Attr.FillColor = Color.LightBlue;
            }

            var publics = graph.Nodes.Where(x => x.LabelText.StartsWith("public"));

            foreach (var node in publics)
            {
                node.Attr.FillColor = Color.LightGreen;
            }
        }
    }
}
