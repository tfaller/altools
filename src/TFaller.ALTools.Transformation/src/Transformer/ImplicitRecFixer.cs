using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.Syntax;
using TFaller.ALTools.Transformation.Rewriter;

namespace TFaller.ALTools.Transformation.Transformer;

/// <summary>
/// Rewriter that makes the implicit "Rec" of codeunits (property TableNo) and pages explicit.
///
/// <code>
/// Name := 'x';           -> Rec.Name := 'x';
/// if Find() then         -> if Rec.Find() then
/// Rec.Name := 'x';       -> unchanged
/// Other.Name := 'x';     -> unchanged
/// </code>
///
/// Identifiers are only prefixed if the semantic model resolves them to a field or procedure of the
/// source table. Own members of the object, local variables, parameters and identifiers inside
/// a "with" statement are left untouched. Field names in the properties ODataKeyFields, SourceTableView, SubPageLink,
/// SubPageView, RunPageLink, RunPageView and TableRelation
/// must stay plain names and are skipped.
/// </summary>
public class ImplicitRecFixer : SyntaxRewriter, IReuseableRewriter
{
    // Properties whose values reference fields by plain name (filter/link syntax), never via Rec
    private static readonly HashSet<string> PlainFieldNameProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "ODataKeyFields", "SourceTableView",
        "SubPageLink", "SubPageView",
        "RunPageLink", "RunPageView",
        "TableRelation"
    };

    private SemanticModel _model = null!;

    public SyntaxNode Rewrite(SyntaxNode node, ref IRewriterContext context)
    {
        _model = context.Model;
        return Visit(node);
    }

    public IReuseableRewriter Clone() => new ImplicitRecFixer();

    public IRewriterContext EmptyContext => new RewriterContext();

    public bool RerunUntilNoChanges => false;

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        if (!IsImplicitRecMember(node))
        {
            return base.VisitIdentifierName(node);
        }

        var rec = SyntaxFactory.IdentifierName("Rec").WithoutTrivia();
        return SyntaxFactory.MemberAccessExpression(rec, node.WithoutTrivia()).WithTriviaFrom(node);
    }

    private bool IsImplicitRecMember(IdentifierNameSyntax node)
    {
        // Rec.Name -> "Name" is a member access, not an implicit one
        if (node.Parent is MemberAccessExpressionSyntax access && access.Name == node)
        {
            return false;
        }

        // Properties like ODataKeyFields or SourceTableView reference fields by plain name
        if (node.Ancestors().OfType<PropertySyntax>().Any(p => PlainFieldNameProperties.Contains(p.Name.Identifier.ValueText ?? "")))
        {
            return false;
        }

        var obj = node.Ancestors().FirstOrDefault(a => a is ObjectSyntax);
        if (obj is not (CodeunitSyntax or PageSyntax or PageExtensionSyntax) ||
            node.Ancestors().Any(a => a is WithStatementSyntax))
        {
            return false;
        }

        var symbol = _model.GetSymbolInfo(node).Symbol;
        return symbol is IFieldSymbol or IMethodSymbol &&
            symbol.ContainingSymbol is ITableTypeSymbol;
    }
}
