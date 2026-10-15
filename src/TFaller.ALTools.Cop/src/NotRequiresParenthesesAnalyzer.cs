namespace TFaller.ALTools.Cop;

using System.Collections.Immutable;
using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.Diagnostics;

/// <summary>
/// In complex boolean expressions "not" must be wrapped in parentheses.
/// "not a and b" is reported, "(not a) and b" is fine.
/// </summary>
[DiagnosticAnalyzer]
public sealed class NotRequiresParenthesesAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "TFALCOP001";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Not operand in a complex boolean expression requires parentheses",
        "Use parentheses around 'not' in a complex boolean expression, e.g. '(not A) and B'",
        "Style",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.UnaryNotExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var node = context.Node;

        // A parenthesized "not" has a ParenthesizedExpression as parent, so only a direct
        // logical operator parent means the "not" is unparenthesized in a complex expression.
        if (node.Parent?.Kind is SyntaxKind.LogicalAndExpression
            or SyntaxKind.LogicalOrExpression
            or SyntaxKind.LogicalXorExpression)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, node.GetLocation()));
        }
    }
}
