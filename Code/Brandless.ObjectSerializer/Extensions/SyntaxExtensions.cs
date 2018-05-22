using Microsoft.CodeAnalysis;

namespace Brandless.ObjectSerializer.Extensions
{
    public static class SyntaxExtensions
    {
        public static TNode NormalizeWhitespace2<TNode>(this TNode node, string indentation = "    ", string eol = "\r\n", bool elasticTrivia = false) where TNode : SyntaxNode
        {
            //return node;
            return (TNode)node.NormalizeWhitespace(indentation, eol, elasticTrivia);
        }
    }
}