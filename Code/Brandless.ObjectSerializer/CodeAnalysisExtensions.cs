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
				GetCommentStatementSyntax(comment),
			});
		}

		private static EmptyStatementSyntax GetCommentStatementSyntax(string comment)
		{
			// Bit hacky, but does the job
			return SyntaxFactory.EmptyStatement()
				.WithSemicolonToken(SyntaxFactory.MissingToken(SyntaxKind.SemicolonToken))
				.WithLeadingTrivia(
			        SyntaxFactory.LineFeed,
                    SyntaxFactory.Comment(
						comment)
				)
                .WithTrailingTrivia(SyntaxFactory.LineFeed);
		}
	}
}