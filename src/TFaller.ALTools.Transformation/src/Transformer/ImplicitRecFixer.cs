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
/// Identifiers are only prefixed if the compiler bound them to the implicitly declared "Rec" variable.
/// Members of other record variables (also inside a "with" statement), own members of the object,
/// local variables, parameters and plain field names in properties (e.g. SourceTableView,
/// SubPageLink) or field arguments (e.g. TestField(Name), SetRange(Name, ...), Validate(Name)) are left untouched.
/// </summary>
public class ImplicitRecFixer : SyntaxRewriter, IReuseableRewriter
{
    private SemanticModel _model = null!;

    public SyntaxNode Rewrite(SyntaxNode node, ref IRewriterContext context)
    {
        _model = context.Model;
        return Visit(node);
    }

    public IReuseableRewriter Clone() => new ImplicitRecFixer();

    public IRewriterContext EmptyContext => new RewriterContext();

    public bool RerunUntilNoChanges => false;

    /// <summary>
    /// Only expression values (Visible, Editable, ...) may use Rec. All other value kinds
    /// (TableRelation, SourceTableView, SubPageLink, CalcFormula, ...) reference fields by
    /// plain name, so they are not visited at all.
    /// </summary>
    public override SyntaxNode? VisitProperty(PropertySyntax node)
    {
        return node.Value is ExpressionPropertyValueSyntax ? base.VisitProperty(node) : node;
    }

    /// <summary>
    /// The name of a page control, e.g. field(Name; Rec.Name), is a declaration and must stay an
    /// identifier even if it is named like a field of Rec, so only the rest is visited.
    /// </summary>
    public override SyntaxNode? VisitPageField(PageFieldSyntax node)
    {
        return node
            .WithExpression((CodeExpressionSyntax)Visit(node.Expression)!)
            .WithPropertyList((PropertyListSyntax)Visit(node.PropertyList)!)
            .WithTriggers(VisitList(node.Triggers));
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        if (!IsImplicitRecMember(node))
        {
            return base.VisitIdentifierName(node);
        }

        var rec = SyntaxFactory.IdentifierName("Rec").WithoutTrivia();
        return SyntaxFactory.MemberAccessExpression(rec, node.WithoutTrivia()).WithTriviaFrom(node);
    }

    /// <summary>
    /// True if the identifier is passed to a parameter that expects a member (field) reference,
    /// e.g. TestField(Name), SetRange(Name, ...), CalcFields(Name), Validate(Name).
    /// </summary>
    private bool IsFieldArgument(IdentifierNameSyntax node)
    {
        if (node.Parent is not ArgumentListSyntax { Parent: InvocationExpressionSyntax invocation })
        {
            return false;
        }

        if (_model.GetOperation(invocation) is not IInvocationExpression call)
        {
            return false;
        }

        return call.Arguments.Any(a => a.Syntax == node && a.Parameter is { IsMemberReference: true });
    }

    private bool IsImplicitRecMember(IdentifierNameSyntax node)
    {
        // Rec.Name -> "Name" is a member access, not an implicit one
        if (node.Parent is MemberAccessExpressionSyntax access && access.Name == node)
        {
            return false;
        }

        // TestField(Name), SetRange(Name, ...), Validate(Name), ... take the field itself
        if (IsFieldArgument(node))
        {
            return false;
        }

        var obj = node.Ancestors().FirstOrDefault(a => a is ObjectSyntax);
        if (obj is not (CodeunitSyntax or PageSyntax or PageExtensionSyntax))
        {
            return false;
        }

        // The identifier is an implicit Rec member if the compiler bound it to the implicitly declared
        // "Rec" variable (a local in codeunits, a global in pages). Members of other variables, also
        // in "with" statements, are bound to that variable instead.
        var instance = _model.GetOperation(node) switch
        {
            IFieldAccess field => field.Instance,
            IInvocationExpression call => call.Instance,
            _ => null,
        };
        var variable = instance switch
        {
            ILocalReferenceExpression local => local.LocalVariable,
            IGlobalReferenceExpression global => global.GlobalVariable,
            _ => null,
        };
        return variable is { IsImplicitlyDeclared: true };
    }
}
