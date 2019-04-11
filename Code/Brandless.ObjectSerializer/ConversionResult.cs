using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Brandless.ObjectSerializer
{
    public class ConversionResult
    {
        public ExpressionSyntax Syntax { get; }
        public bool DidConvert { get; }

        public ConversionResult(ExpressionSyntax syntax, bool didConvert)
        {
            Syntax = syntax;
            DidConvert = didConvert;
        }
    }
}