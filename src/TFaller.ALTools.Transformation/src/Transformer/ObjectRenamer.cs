using System.Collections.Generic;
using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.Syntax;
using TFaller.ALTools.Transformation.Rewriter;

namespace TFaller.ALTools.Transformation.Transformer;

/// <summary>
/// Rewriter that renames codeunits, tables and pages.
/// The declarations and every identifier the semantic model resolves to a configured object are updated
/// (variable types, Database::X / Page::X / Codeunit::X, SourceTable, RunObject, ...).
/// [EventSubscriber] attributes are handled as well.
/// Not updated: names in plain strings.
/// </summary>
public class ObjectRenamer : SyntaxRewriter, IReuseableRewriter
{
    public enum ObjectKind
    {
        Codeunit,
        Table,
        Page,
    }

    /// <summary>
    /// Rename of one object.
    /// </summary>
    /// <param name="Kind">Kind of the object</param>
    /// <param name="OldName">Current name</param>
    /// <param name="NewName">New name (null keeps the name)</param>
    /// <param name="NewNo">New object number (null keeps the number)</param>
    public record ObjectRename(ObjectKind Kind, string OldName, string? NewName = null, int? NewNo = null);

    private readonly List<ObjectRename> _renames;
    private readonly Dictionary<(ObjectKind, string), ObjectRename> _map = [];
    private SemanticModel _model = null!;

    public ObjectRenamer(IEnumerable<ObjectRename> renames)
    {
        _renames = [.. renames];
        foreach (var r in _renames)
        {
            _map[(r.Kind, Key(r.OldName))] = r;
        }
    }

    public IRewriterContext EmptyContext => new RewriterContext();

    public bool RerunUntilNoChanges => false;

    public IReuseableRewriter Clone() => new ObjectRenamer(_renames);

    public SyntaxNode Rewrite(SyntaxNode node, ref IRewriterContext context)
    {
        _model = context.Model;
        return Visit(node);
    }

    private static string Key(string name) => Formatter.UnquoteIdentifier(name).ToUpperInvariant();

    private bool TryGetRename(ObjectKind kind, string? name, out ObjectRename rename)
    {
        rename = null!;
        return name is not null && _map.TryGetValue((kind, Key(name)), out rename!);
    }

    private static ObjectKind? KindOf(ISymbol? symbol) => symbol switch
    {
        ICodeunitTypeSymbol => ObjectKind.Codeunit,
        ITableTypeSymbol => ObjectKind.Table,
        IPageTypeSymbol => ObjectKind.Page,
        _ => null,
    };

    public override SyntaxNode? VisitCodeunit(CodeunitSyntax node)
    {
        var result = (CodeunitSyntax)base.VisitCodeunit(node)!;
        if (!TryGetRename(ObjectKind.Codeunit, node.Name.Identifier.ValueText, out var r))
        {
            return result;
        }

        if (r.NewName is { } n)
        {
            result = result.WithName(Rename(result.Name, n));
        }

        if (r.NewNo is { } no)
        {
            result = result.WithObjectId(SyntaxFactory.ObjectId(SyntaxFactory.Literal(no).WithTriviaFrom(result.ObjectId.Value)));
        }

        return result;
    }

    public override SyntaxNode? VisitTable(TableSyntax node)
    {
        var result = (TableSyntax)base.VisitTable(node)!;
        if (!TryGetRename(ObjectKind.Table, node.Name.Identifier.ValueText, out var r))
        {
            return result;
        }

        if (r.NewName is { } n)
        {
            result = result.WithName(Rename(result.Name, n));
        }

        if (r.NewNo is { } no)
        {
            result = result.WithObjectId(SyntaxFactory.ObjectId(SyntaxFactory.Literal(no).WithTriviaFrom(result.ObjectId.Value)));
        }

        return result;
    }

    public override SyntaxNode? VisitPage(PageSyntax node)
    {
        var result = (PageSyntax)base.VisitPage(node)!;
        if (!TryGetRename(ObjectKind.Page, node.Name.Identifier.ValueText, out var r))
        {
            return result;
        }

        if (r.NewName is { } n)
        {
            result = result.WithName(Rename(result.Name, n));
        }

        if (r.NewNo is { } no)
        {
            result = result.WithObjectId(SyntaxFactory.ObjectId(SyntaxFactory.Literal(no).WithTriviaFrom(result.ObjectId.Value)));
        }

        return result;
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        var symbol = _model.GetSymbolInfo(node).Symbol;
        if (KindOf(symbol) is { } kind && TryGetRename(kind, symbol!.Name, out var rename) && rename.NewName is { } newName)
        {
            return Rename(node, newName);
        }

        return base.VisitIdentifierName(node);
    }

    private static IdentifierNameSyntax Rename(IdentifierNameSyntax node, string newName)
        => node.WithIdentifier(SyntaxFactory.Identifier(Formatter.QuoteIdentifier(newName)).WithTriviaFrom(node.Identifier));
}
