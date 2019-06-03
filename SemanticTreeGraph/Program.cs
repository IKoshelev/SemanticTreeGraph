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
        static async Task Main(string[] args)
        {
            Graph graph = new Graph("");

            var settings = graph.LayoutAlgorithmSettings as SugiyamaLayoutSettings;

            settings.Transformation = PlaneTransformation.Rotation(Math.PI / 2);

            await Analyze(graph);

            graph.Write("graph");

            //create a form 
            Form form = new Form();
            form.Width = 1024;
            form.Height = 768;
            //create a viewer object 
            GViewer viewer = new GViewer();

            viewer.Graph = graph;
            //associate the viewer with the form 
            form.SuspendLayout();
            viewer.Dock = DockStyle.Fill;
            form.Controls.Add(viewer);
            form.ResumeLayout();
            //show the form 
            form.ShowDialog();

            //ToBitmap(graph);
        }

        private static async Task Analyze(Graph graph)
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

                // Attach progress reporter so we print projects as they are loaded.
                var solution = await workspace.OpenSolutionAsync(solutionPath, new ConsoleProgressReporter());

                new GraphBuilder().Build(graph, solution);
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
