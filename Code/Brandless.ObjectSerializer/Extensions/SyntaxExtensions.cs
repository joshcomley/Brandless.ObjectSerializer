using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Brandless.ObjectSerializer.Extensions
{
    public static class SyntaxExtensions
    {
        internal static InternalConversionResult ToResult(this ExpressionSyntax syntax)
        {
            return new InternalConversionResult(syntax);
        }

        public static TNode NormalizeWhitespace2<TNode>(this TNode node, string indentation = "    ", string eol = "\r\n", bool elasticTrivia = false) where TNode : SyntaxNode
        {
            //return node;
            return (TNode)node.NormalizeWhitespace(indentation, eol, elasticTrivia);
        }
    }
}