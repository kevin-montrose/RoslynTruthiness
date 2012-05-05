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
        static Dictionary<string, SyntaxTree> DeTruthyPass(Dictionary<string, SyntaxTree> trees, IEnumerable<AssemblyFileReference> references, out bool didReplace)
        {
            didReplace = false;

            var newTrees = new Dictionary<string, SyntaxTree>();

            var comp = 
                Compilation.Create(
                    "Dummy",
                    syntaxTrees: trees.Select(x => x.Value),
                    references: references
                );

            var rewrittenTrees = new Dictionary<string, SyntaxNode>();

            foreach (var kv in trees)
            {
                var tree = kv.Value;

                var finder = new FindTruthy(comp.GetSemanticModel(tree));

                finder.Visit(tree.Root);

                var rewritten = tree.Root;

                if (finder.AreTruthy.Count() > 0)
                {
                    bool innerDidReplace = false;

                    rewritten =
                        rewritten.ReplaceNodes(finder.AreTruthy,
                            (node, node2) =>
                            {
                                innerDidReplace = true;

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

                    didReplace = didReplace || innerDidReplace;
                }

                newTrees[kv.Key] = SyntaxTree.Create(kv.Value.FileName, (CompilationUnitSyntax)rewritten);
            }

            return newTrees;
        }

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

            Dictionary<string, SyntaxTree> rewrittenTrees = trees;

            bool goAgain = true;

            while(goAgain)
            {
                goAgain = false;
                rewrittenTrees = DeTruthyPass(rewrittenTrees, references, out goAgain);
            }

            var adder = new AddTruthinessMethods();

            foreach (var file in rewrittenTrees)
            {
                var outfile = Path.Combine(outDir, file.Key);
                
                if(!Directory.GetParent(outfile).Exists)
                {
                    Directory.CreateDirectory(Directory.GetParent(outfile).FullName);
                }

                var afterAdder = adder.Visit(file.Value.Root);

                using(var stream = new StreamWriter(File.OpenWrite(outfile)))
                {
                    afterAdder.WriteTo(stream);
                }
            }

            // Move the project file
            File.Copy(csproj, Path.Combine(outDir, Path.GetFileName(csproj)), overwrite: true);

            return 0;
        }
    }
}