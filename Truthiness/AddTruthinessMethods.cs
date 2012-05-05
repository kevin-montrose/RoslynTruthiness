using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Roslyn.Compilers.CSharp;

namespace Truthiness
{
    class AddTruthinessMethods : SyntaxRewriter
    {
        private static MemberDeclarationSyntax TruthyMethod;

        static AddTruthinessMethods()
        {
            SyntaxTree parsed =
                SyntaxTree.ParseCompilationUnit(
@"        private static bool __Truthy(object o)
        {
            if (o == null || (o is string && (string)o == string.Empty)) return false;

            var type = o.GetType();

            if (type.IsValueType)
            {
                return !o.Equals(Activator.CreateInstance(type));
            }

            return true;
        }
"
                );

            TruthyMethod = parsed.Root.DescendentNodesAndSelf().OfType<MethodDeclarationSyntax>().Single();
        }

        protected override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var callsTruthiness =
                node.DescendentNodes().OfType<InvocationExpressionSyntax>()
                .Select(i => i.Expression).OfType<IdentifierNameSyntax>()
                .Any(a => a.PlainName == "__Truthy");

            var allMethods = node.DescendentNodes().OfType<MethodDeclarationSyntax>().ToList();

            if (callsTruthiness)
            {
                SyntaxList<MemberDeclarationSyntax> newMembers = Syntax.List(node.Members.Union(new[] { TruthyMethod }));

                return node.Update(node.Attributes, node.Modifiers, node.Keyword, node.Identifier, node.TypeParameterListOpt, node.BaseListOpt, node.ConstraintClauses, node.OpenBraceToken, newMembers, node.CloseBraceToken, node.SemicolonTokenOpt);
            }

            return base.VisitClassDeclaration(node);
        }
    }
}
