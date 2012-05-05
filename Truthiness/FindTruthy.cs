using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Roslyn.Compilers.CSharp;

namespace Truthiness
{
    class FindTruthy : SyntaxWalker
    {
        private List<ExpressionSyntax> _AreTruthy = new List<ExpressionSyntax>();
        public IEnumerable<ExpressionSyntax> AreTruthy { get { return _AreTruthy; } }

        private SemanticModel Model;

        public FindTruthy(SemanticModel model)
        {
            Model = model;
        }

        protected override void VisitConditionalExpression(ConditionalExpressionSyntax node)
        {
            Consider(node.Condition);

            base.VisitConditionalExpression(node);
        }

        protected override void VisitIfStatement(IfStatementSyntax node)
        {
            Consider(node.Condition);

            base.VisitIfStatement(node);
        }

        protected override void VisitDoStatement(DoStatementSyntax node)
        {
            Consider(node.Condition);

            base.VisitDoStatement(node);
        }

        protected override void VisitWhileStatement(WhileStatementSyntax node)
        {
            Consider(node.Condition);

            base.VisitWhileStatement(node);
        }

        protected override void VisitForStatement(ForStatementSyntax node)
        {
            if (node.ConditionOpt != null)
            {
                Consider(node.ConditionOpt);
            }

            base.VisitForStatement(node);
        }

        private bool IsBool(ExpressionSyntax exp)
        {
            var info = Model.GetSemanticInfo(exp);
            var knownType = info.Type;

            return 
                knownType != null && 
                knownType.SpecialType == Roslyn.Compilers.SpecialType.System_Boolean;
        }

        private void Add(ExpressionSyntax exp)
        {
            _AreTruthy.Add(exp);
        }

        private bool Consider(ExpressionSyntax exp)
        {
            var paren = exp as ParenthesizedExpressionSyntax;
            if (paren != null)
            {
                return Consider(paren.Expression);
            }

            var binary = exp as BinaryExpressionSyntax;
            if (binary != null)
            {
                var left = binary.Left;
                var right = binary.Right;

                switch (binary.OperatorToken.Kind)
                {
                    // ||, |, &, &&
                    case SyntaxKind.BarBarToken:
                    case SyntaxKind.BarToken:
                    case SyntaxKind.AmpersandToken:
                    case SyntaxKind.AmpersandAmpersandToken:
                        if (!IsBool(left))
                        {
                            if (!Consider(left))
                            {
                                Add(left);
                            }
                        }
                        if (!IsBool(right))
                        {
                            if (!Consider(right))
                            {
                                Add(right);
                            }
                        }
                        break;

                    default:
                        if (!IsBool(exp)) Add(exp);
                        break;
                }

                return true;
            }

            var accessor = exp as MemberAccessExpressionSyntax;
            if (accessor != null)
            {
                if (!IsBool(accessor)) Add(accessor);

                return true;
            }

            var identifier = exp as IdentifierNameSyntax;
            if (identifier != null)
            {
                if (!IsBool(identifier)) Add(identifier);

                return true;
            }

            var elemAccess = exp as ElementAccessExpressionSyntax;
            if (elemAccess != null)
            {
                if (!IsBool(elemAccess.Expression)) Add(elemAccess);

                return true;
            }

            var literal = exp as LiteralExpressionSyntax;
            if (literal != null)
            {
                if (!IsBool(literal)) Add(literal);

                return true;
            }

            var unary = exp as PrefixUnaryExpressionSyntax;
            if (unary != null)
            {
                // !
                if (unary.OperatorToken.Kind == SyntaxKind.ExclamationToken)
                {
                    if (!IsBool(unary.Operand))
                    {
                        if (!Consider(unary.Operand))
                        {
                            Add(unary.Operand);
                        }
                    }
                }

                return true;
            }

            return false;
        }
    }
}