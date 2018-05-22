using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Brandless.ObjectSerializer
{
    public class ObjectToObjectSerializeOutput
    {
        public string Class { get; set; }
        public string Initialiser { get; set; }
        public ExpressionSyntax InitialiserSyntax { get; set; }
        public string Instance { get; set; }
        public LocalDeclarationStatementSyntax InstanceSyntax { get; set; }
        public CompilationUnitSyntax CompilationUnit { get; set; }

        public ObjectToObjectSerializeOutput()
        {
        }
    }
}