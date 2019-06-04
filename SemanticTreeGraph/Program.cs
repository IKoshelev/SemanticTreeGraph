using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Msagl.Core.Geometry.Curves;
using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.GraphViewerGdi;
using Microsoft.Msagl.Layout.Layered;
using Color = Microsoft.Msagl.Drawing.Color;

namespace SemanticTreeGraph
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Graph originalGraph = new Graph("");

            LoadSolutionAnnBuildGrpah(originalGraph).Wait();

            while (true)
            {
                var graph = CopyGraph(originalGraph);
                //  the nodes you are interested in
                var startNodes = new HashSet<string>()
                {
                    "ABCD",
                    //"GetBC"
                };
                // the nodes with lots of connection, that would 
                // pull in entire file
                // todo try to autodetect? 
                var nodesToNotContinue = new HashSet<string>()
                {
                    //"GetAB"
                };

                TrimGraph(graph, startNodes, nodesToNotContinue);

                DisplayGraph(graph);
            }
        }

        private static void TrimGraph(Graph graph, HashSet<string> startNodes, HashSet<string> nodesToNotContinue)
        {
            var nodesToLeaveInTheGraph = new HashSet<Node>();

            startNodes
                .Select(graph.FindNode)
                .ForEach(x => nodesToLeaveInTheGraph.Add(x));

            var nodesToProcess = new HashSet<Node>(nodesToLeaveInTheGraph);

            while (nodesToProcess.Any())
            {
                var currentNodes = nodesToProcess;
                nodesToProcess = new HashSet<Node>();
                currentNodes.ForEach(node =>
                {
                    if (nodesToNotContinue.Contains(node.Id))
                    {
                        node.Attr.AddStyle(Style.Dashed);
                        return;
                    }

                    var dependentNodes = node.OutEdges
                                            .Select(x => x.TargetNode)
                                            // avoid SelfEdge
                                            .Where(x => nodesToLeaveInTheGraph.Contains(x) == false)
                                            .ToArray();

                    dependentNodes.ForEach(newNode =>
                    {
                        nodesToProcess.Add(newNode);
                        nodesToLeaveInTheGraph.Add(newNode);
                    });
                });
            }

            graph.Nodes.ToArray().ForEach(x =>
            {
                if (nodesToLeaveInTheGraph.Contains(x) == false)
                {
                    try
                    {
                        var n = graph.FindNode(x.Id);
                        graph.RemoveNode(n);
                    }
                    catch
                    {
                        //code above throws, even though it looks like it should not
                        //still does the job though
                    }
                }
            });
        }

        private static Graph CopyGraph(Graph original)
        {
            using (var stream = new MemoryStream())
            {
                original.WriteToStream(stream);
                stream.Position = 0;
                return Graph.ReadGraphFromStream(stream);
            }
        }

        private static void DisplayGraph(Graph graph)
        {
            var settings = graph.LayoutAlgorithmSettings as SugiyamaLayoutSettings;

            settings.Transformation = PlaneTransformation.Rotation(Math.PI / 2);

            Form form = new Form();
            form.Width = 1024;
            form.Height = 768;

            GViewer viewer = new GViewer();
            viewer.Graph = graph;

            form.SuspendLayout();
            viewer.Dock = DockStyle.Fill;
            form.Controls.Add(viewer);
            form.ResumeLayout();

            form.ShowDialog();
        }

        private static async Task LoadSolutionAnnBuildGrpah(Graph graph)
        {
            var visualStudioInstances = MSBuildLocator.QueryVisualStudioInstances().ToArray();
            var instance = visualStudioInstances[0];

            Console.WriteLine($"Using MSBuild at '{instance.MSBuildPath}' to load projects.");
            MSBuildLocator.RegisterInstance(instance);

            using (var workspace = MSBuildWorkspace.Create())
            {
                // Print message for WorkspaceFailed event to help diagnosing project load failures.
                workspace.WorkspaceFailed += (o, e) => Console.WriteLine(e.Diagnostic.Message);

                var solutionPath = @"C:\Users\Me\Documents\Visual Studio 2017\Projects\Test-bed\Test-bed.sln";
                Console.WriteLine($"Loading solution '{solutionPath}'");

                var solution = await workspace.OpenSolutionAsync(solutionPath, new ConsoleProgressReporter());
                var project = solution.Projects.Single(x => x.FilePath == @"C:\Users\Me\Documents\Visual Studio 2017\Projects\Test-bed\Test-bed\Test-bed.csproj");
                var doc = project.Documents.Single(x => x.Name == "Class1.cs");

                var buildTask = new GraphBuilder().Build(graph, solution, project, doc);
                buildTask.Wait();
            }
        }

        private static void ToBitmap(Graph graph)
        {           
            GraphRenderer renderer = new GraphRenderer(graph);

            renderer.CalculateLayout();
           
            int width = 1000;
            Bitmap bitmap = new Bitmap(width, 
                                        (int)(graph.Height * (width / graph.Width)), 
                                        System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            renderer.Render(bitmap);
            bitmap.Save("test.png");
        }

        private class ConsoleProgressReporter : IProgress<ProjectLoadProgress>
        {
            public void Report(ProjectLoadProgress loadProgress)
            {
                var projectDisplay = Path.GetFileName(loadProgress.FilePath);
                if (loadProgress.TargetFramework != null)
                {
                    projectDisplay += $" ({loadProgress.TargetFramework})";
                }

                Console.WriteLine($"{loadProgress.Operation,-15} {loadProgress.ElapsedTime,-15:m\\:ss\\.fffffff} {projectDisplay}");
            }
        }
    }
}
