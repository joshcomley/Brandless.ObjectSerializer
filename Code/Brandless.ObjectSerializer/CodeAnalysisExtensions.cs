using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Brandless.ObjectSerializer
{
    internal static class CodeAnalysisExtensions
    {
        public static IEnumerable<StatementSyntax> AddComment(this IEnumerable<StatementSyntax> statementSyntax, string comment)
        {
            return statementSyntax.Union(new StatementSyntax[]
            {
                ToCommentStatementSyntax(comment),
            });
        }

        internal static EmptyStatementSyntax ToCommentStatementSyntax(this string comment, bool onOwnLine = true)
        {
            // Bit hacky, but does the job
            if (onOwnLine)
            {
                return SyntaxFactory.EmptyStatement()
                    .WithSemicolonToken(SyntaxFactory.MissingToken(SyntaxKind.SemicolonToken))
                    .WithLeadingTrivia(
                        SyntaxFactory.CarriageReturnLineFeed,
                        SyntaxFactory.Comment(
                            comment)
                    )
                    .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);
            }
            return SyntaxFactory.EmptyStatement()
                .WithSemicolonToken(SyntaxFactory.MissingToken(SyntaxKind.SemicolonToken))
                .WithLeadingTrivia(
                    SyntaxFactory.Comment(
                        comment)
                );
        }
    }
}