using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Roslyn.Compilers;
using Roslyn.Compilers.CSharp;
using System.IO;

namespace Truthiness
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Expected <project file> <output directory> as parameters");
                return -1;
            }

            var csproj = args[0];
            var outDir = args[1];

            if (!File.Exists(csproj))
            {
                Console.WriteLine("Could not find <" + csproj + ">");
                return -1;
            }

            if (!Directory.Exists(outDir))
            {
                Console.WriteLine("<" + outDir + "> doesn't exist");
                return -1;
            }

            List<string> files;
            List<AssemblyFileReference> references;
            ProjectParser.ParseProject(csproj, out files, out references);

            var dir = Directory.GetParent(csproj);

            var trees =
                files.ToDictionary(
                    csFile => csFile,
                    delegate(string csFile)
                    {
                        var code = File.ReadAllText(Path.Combine(dir.FullName, csFile));

                        return SyntaxTree.ParseCompilationUnit(code);
                    }
                );

            var comp = 
                Compilation.Create(
                    "Dummy",
                    syntaxTrees: trees.Select(x => x.Value),
                    references: references
                );

            var rewrittenTrees = new Dictionary<string, SyntaxNode>();

            var adder = new AddTruthinessMethods();

            foreach (var kv in trees)
            {
                var tree = kv.Value;

                var finder = new FindTruthy(comp.GetSemanticModel(tree));

                finder.Visit(tree.Root);

                var rewritten = tree.Root;

                if (finder.AreTruthy.Count() > 0)
                {
                    rewritten =
                        rewritten.ReplaceNodes(finder.AreTruthy,
                            (node, node2) =>
                            {
                                var arg = Syntax.Argument(expression: node);

                                var trailing = arg.HasTrailingTrivia ? arg.GetTrailingTrivia() : default(SyntaxTriviaList);
                                var leading = arg.HasLeadingTrivia ? arg.GetLeadingTrivia() : default(SyntaxTriviaList);

                                var ret =
                                    Syntax.InvocationExpression(
                                        Syntax.IdentifierName("__Truthy"),
                                        Syntax.ArgumentList(Syntax.Token(SyntaxKind.OpenParenToken), Syntax.SeparatedList(arg.WithLeadingTrivia().WithTrailingTrivia()), Syntax.Token(SyntaxKind.CloseParenToken))
                                    );

                                ret = ret.WithLeadingTrivia(leading).WithTrailingTrivia(trailing);

                                return ret;
                            }
                        );

                    rewritten = adder.Visit(rewritten);
                }

                rewrittenTrees[kv.Key] = rewritten;
            }

            foreach (var file in rewrittenTrees)
            {
                var outfile = Path.Combine(outDir, file.Key);
                
                if(!Directory.GetParent(outfile).Exists)
                {
                    Directory.CreateDirectory(Directory.GetParent(outfile).FullName);
                }

                using(var stream = new StreamWriter(File.OpenWrite(outfile)))
                {
                    file.Value.WriteTo(stream);
                }
            }

            // Move the project file
            File.Copy(csproj, Path.Combine(outDir, Path.GetFileName(csproj)), overwrite: true);

            return 0;
        }
    }
}