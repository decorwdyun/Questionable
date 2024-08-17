using Microsoft.CodeAnalysis.CSharp.Syntax;
using Questionable.Model.Questing;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Questionable.QuestPathGenerator.RoslynShortcuts;

namespace Questionable.QuestPathGenerator.RoslynElements;

internal static class LeveIdExtensions
{
    public static ExpressionSyntax ToExpressionSyntax(this LeveId leveId)
    {
        return ObjectCreationExpression(
                IdentifierName(nameof(LeveId)))
            .WithArgumentList(
                ArgumentList(
                    SingletonSeparatedList(
                        Argument(LiteralValue(leveId.Value)))));
    }
}
