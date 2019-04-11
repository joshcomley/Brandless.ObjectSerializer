using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Brandless.ObjectSerializer
{
    internal class InternalConversionResult : ConversionResult
    {
        public InternalConversionResult(ExpressionSyntax syntax) : base(syntax, true)
        {
        }
    }
}