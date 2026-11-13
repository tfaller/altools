using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.Syntax;
using TFaller.ALTools.Transformation.Rewriter;

namespace TFaller.ALTools.Transformation.Transformer;

/// <summary>
/// New name and/or number of a table field. Unset values are kept as they are.
/// </summary>
/// <param name="Name">New field name</param>
/// <param name="No">New field number</param>
public record TableFieldChange(string? Name = null, int? No = null);

/// <summary>
/// Changes of the fields of one table.
/// </summary>
/// <param name="Table">Name of the table</param>
/// <param name="Fields">Current field name -> change</param>
public record TableFieldChanges(string Table, IReadOnlyDictionary<string, TableFieldChange> Fields);

/// <summary>
/// Rewriter that renames and/or renumbers fields of tables.
/// All declarations (table and table extensions) and all references to the fields are updated.
/// Fields that are not configured are ignored.
///
/// Updated are:
/// - field declarations (name, number) in the table and in table extensions of the table
/// - every identifier the semantic model resolves to a configured field
/// - [EventSubscriber] attributes that name a configured field of a table
///
/// Not updated, because they can't be resolved statically: field numbers used as literals
/// (e.g. RecordRef.Field(1)) and field names used in plain strings.
/// </summary>
public class TableFieldRenamer : SyntaxRewriter, IReuseableRewriter
{
    private readonly Dictionary<string, Dictionary<string, TableFieldChange>> _tables;
    private SemanticModel _model = null!;

    public TableFieldRenamer(IEnumerable<TableFieldChanges> tables)
    {
        _tables = new(StringComparer.OrdinalIgnoreCase);
        foreach (var table in tables)
        {
            var key = Formatter.UnquoteIdentifier(table.Table);
            if (!_tables.TryGetValue(key, out var fields))
            {
                _tables[key] = fields = new(StringComparer.OrdinalIgnoreCase);
            }

            foreach (var (name, change) in table.Fields)
            {
                fields[Formatter.UnquoteIdentifier(name)] = change;
            }
        }
    }

    public IRewriterContext EmptyContext => new RewriterContext();

    public bool RerunUntilNoChanges => false;

    public IReuseableRewriter Clone() => new TableFieldRenamer(
        _tables.Select(t => new TableFieldChanges(t.Key, t.Value)));

    public SyntaxNode Rewrite(SyntaxNode node, ref IRewriterContext context)
    {
        _model = context.Model;
        return Visit(node);
    }

    public override SyntaxNode? VisitField(FieldSyntax node)
    {
        var result = (FieldSyntax)base.VisitField(node)!;

        var table = GetDeclaringTableName(node);
        if (table is null || !TryGetChange(table, node.Name.Identifier.ValueText ?? string.Empty, out var change))
        {
            return result;
        }

        if (change.Name is { } newName)
        {
            // not every declaration is resolved by the semantic model (e.g. fields of table extensions)
            result = result.WithName(RenameIdentifier(result.Name, newName));
        }

        if (change.No is { } no)
        {
            result = result.WithNo(SyntaxFactory.Literal(no).WithTriviaFrom(result.No));
        }

        return result;
    }

    /// <summary>
    /// Adjusts [EventSubscriber(ObjectType::Table, Database::Table, 'OnAfterValidateEvent', 'Field')],
    /// which names the field as string.
    /// </summary>
    public override SyntaxNode? VisitMemberAttribute(MemberAttributeSyntax node)
    {
        var result = (MemberAttributeSyntax)base.VisitMemberAttribute(node)!;

        if (!"EventSubscriber".EqualsOrdinalIgnoreCase(node.Name.Identifier.ValueText) ||
            node.ArgumentList?.Arguments is not { Count: >= 4 } args ||
            args[0] is not OptionAccessAttributeArgumentSyntax { OptionAccess: { } objectType } ||
            !"Table".EqualsOrdinalIgnoreCase(objectType.Name.ToString().Trim()) ||
            args[1] is not OptionAccessAttributeArgumentSyntax { OptionAccess: { } database } ||
            !"Database".EqualsOrdinalIgnoreCase(database.Expression.ToString().Trim()) ||
            args[3] is not LiteralAttributeArgumentSyntax ||
            GetStringLiteralValue(args[3]) is not { } field ||
            !TryGetChange(database.Name.ToString().Trim(), field, out var change) ||
            change.Name is not { } newName)
        {
            return result;
        }

        var literal = SyntaxFactory.LiteralAttributeArgument(SyntaxFactory.LiteralExpression(
                SyntaxFactory.StringLiteralValue(SyntaxFactory.Literal(newName))))
            .WithTriviaFrom(args[3]);

        return result.ReplaceNode(result.ArgumentList!.Arguments[3], literal);
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        if (!TryGetFieldChange(node, out var change) || change.Name is not { } newName)
        {
            return base.VisitIdentifierName(node);
        }

        return RenameIdentifier(node, newName);
    }

    private static string? GetStringLiteralValue(SyntaxNode literal)
    {
        var text = literal.ToString().Trim();
        return text.Length >= 2 && text[0] == '\'' && text[^1] == '\''
            ? text[1..^1].Replace("''", "'")
            : null;
    }

    private static IdentifierNameSyntax RenameIdentifier(IdentifierNameSyntax node, string newName)
    {
        var token = node.Identifier;
        var newToken = SyntaxFactory.Identifier(Formatter.QuoteIdentifier(newName))
            .WithTriviaFrom(token);
        return node.WithIdentifier(newToken);
    }

    private bool TryGetFieldChange(IdentifierNameSyntax node, out TableFieldChange change)
    {
        change = null!;

        var symbol = _model.GetSymbolInfo(node).Symbol;
        if (symbol is IFieldSymbol field)
        {
            var table = GetTableName(field);
            return table is not null && TryGetChange(table, field.Name ?? string.Empty, out change);
        }

        // The semantic model does not resolve the fields on the left side of table filters.
        // Like: CalcFormula = count(Table where(Field = field(Other))) or TableRelation = Table where(Field = const(1))
        if (symbol is null && GetFilterTableName(node) is { } filterTable)
        {
            return TryGetChange(filterTable, node.Identifier.ValueText ?? string.Empty, out change);
        }

        return false;
    }

    /// <summary>
    /// Table of a field identifier that is part of a table filter, which the semantic model does not resolve.
    /// Like: CalcFormula = count(Table where(Field = field(Other))), TableRelation = Table.Field where(...),
    /// SubPageLink = Field = field(Other). Null if the node is no such identifier.
    /// </summary>
    private string? GetFilterTableName(IdentifierNameSyntax node)
    {
        // TableRelation = Table.Field
        if (node.Parent is QualifiedNameSyntax qualifiedName && qualifiedName.ChildNodes().Last() == node)
        {
            return qualifiedName.ChildNodes().First().ToString().Trim();
        }

        if (node.Parent is not (SimpleFieldExpressionSyntax or ConstExpressionSyntax or FilterExpressionSyntax) ||
            node.Parent.Parent is not (TableFilterExpressionSyntax or TableFilterPropertyValueSyntax))
        {
            return null;
        }

        var filter = node.Parent.Parent;

        // Left side: Field of the filtered table, right side (field(X)): field of the current table
        var isLeft = node.Parent.ChildNodes().First() == node;

        // SubPageLink = Field = field(Other)
        if (filter is TableFilterPropertyValueSyntax)
        {
            var page = isLeft ? filter.Ancestors().OfType<PagePartSyntax>().FirstOrDefault()?
                .ChildNodes().OfType<ObjectNameOrIdSyntax>().FirstOrDefault()?.ChildNodes().FirstOrDefault()
                : filter.Ancestors().FirstOrDefault(a => a is PageSyntax or PageExtensionSyntax);
            return page is null ? null : GetPageTableName(page);
        }

        if (!isLeft)
        {
            return GetDeclaringTableName(node);
        }

        switch (filter.Parent)
        {
            // if (Field = const(1)) -> field of the table that declares the relation
            case IfTableRelationSyntax:
                return GetDeclaringTableName(node);

            // Table.Field where (Field = ...) / count(Table where (Field = ...))
            case WhereExpressionSyntax where:
                // the first name is the table, "if (...)" conditions can come before it
                var source = where.Parent?.ChildNodes().FirstOrDefault(n => n is QualifiedNameSyntax or IdentifierNameSyntax);
                return source switch
                {
                    QualifiedNameSyntax qualified => qualified.ChildNodes().First().ToString().Trim(),
                    IdentifierNameSyntax identifier => identifier.ToString().Trim(),
                    _ => null,
                };

            default:
                return null;
        }
    }

    /// <summary>
    /// Source table of a page, given either the page object or a reference (identifier) to it.
    /// </summary>
    private string? GetPageTableName(SyntaxNode page)
    {
        ISymbol? symbol = page is ObjectSyntax
            ? _model.GetDeclaredSymbol(page)
            : _model.GetSymbolInfo(page).Symbol;
        return (symbol as IPageTypeSymbol)?.RelatedTable?.Name;
    }

    private bool TryGetChange(string table, string field, out TableFieldChange change)
    {
        change = null!;
        return _tables.TryGetValue(Formatter.UnquoteIdentifier(table), out var fields)
            && fields.TryGetValue(Formatter.UnquoteIdentifier(field), out change!);
    }

    private static string? GetTableName(IFieldSymbol field)
    {
        return field.ContainingSymbol switch
        {
            ITableTypeSymbol table => table.Name,
            ITableExtensionTypeSymbol ext => ext.Target?.Name,
            _ => null,
        };
    }

    private static string? GetDeclaringTableName(SyntaxNode field)
    {
        return field.Ancestors().FirstOrDefault(a => a is ObjectSyntax) switch
        {
            TableSyntax table => table.Name.Identifier.ValueText,
            TableExtensionSyntax ext => ext.BaseObject.ToString().Trim(),
            _ => null,
        };
    }
}
