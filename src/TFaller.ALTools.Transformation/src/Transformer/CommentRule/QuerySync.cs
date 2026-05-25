using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.Symbols;
using Microsoft.Dynamics.Nav.CodeAnalysis.Syntax;
using Microsoft.Dynamics.Nav.CodeAnalysis.Utilities;
using TFaller.ALTools.Transformation.Rewriter;

namespace TFaller.ALTools.Transformation.Transformer.CommentRule;

/// <summary>
/// Rewriter that keeps AL Query object columns in sync with a source table.
///
/// Annotation format (placed in the leading trivia of the query object):
///   // @altools:transform:&lt;tag&gt;:query-sync:&lt;TableName&gt;
///   // - ExcludedField1
///   // - "Excluded Field 2"
///
/// Fields with FieldClass = FlowField or FlowFilter, and disabled fields, are
/// automatically excluded.
/// </summary>
public partial class QuerySync : IReuseableRewriter
{
    [GeneratedRegex(@"@altools:transform:([^:]+):query-sync:(.+)$", RegexOptions.Compiled)]
    public static partial Regex AnnotationRegex();

    [GeneratedRegex(@"^\s*//\s*-\s+(.+?)\s*$", RegexOptions.Compiled)]
    public static partial Regex BlacklistLineRegex();

    [GeneratedRegex(@"[^a-zA-Z0-9]")]
    private static partial Regex NonAlphaNumericRegex();

    private static readonly char[] WordSeperator = [' ', '.', '-', '_'];

    private SemanticModel _model = null!;
    private readonly HashSet<string> _activeTags;

    public QuerySync(HashSet<string>? activeTags = null)
    {
        _activeTags = activeTags ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public record QueryAnnotation(string Tag, string TableName, ImmutableHashSet<string> Blacklist);

    public SyntaxNode Rewrite(SyntaxNode node, ref IRewriterContext context)
    {
        _model = context.Model;
        return ProcessQueries(node);
    }

    public IReuseableRewriter Clone() => new QuerySync(_activeTags);

    // No cross-file state needed: table types are resolved directly via the semantic model.
    public IRewriterContext EmptyContext => new RewriterContext();

    public bool RerunUntilNoChanges => false;

    private SyntaxNode ProcessQueries(SyntaxNode root)
    {
        var queries = root.DescendantNodes().OfType<QuerySyntax>().ToList();
        if (queries.Count == 0)
            return root;

        SyntaxNode result = root;
        foreach (var query in queries)
        {
            var annotation = ParseAnnotation(query);
            if (annotation is null || !IsTagActive(annotation.Tag))
                continue;

            var newQuery = SyncQuery(query, annotation);
            if (newQuery != query)
                result = result.ReplaceNode(query, newQuery);
        }

        return result;
    }

    private QuerySyntax SyncQuery(QuerySyntax query, QueryAnnotation annotation)
    {
        var dataItems = query.DescendantNodes().OfType<QueryDataItemSyntax>().ToList();
        if (dataItems.Count == 0)
            return query;

        QuerySyntax result = query;
        foreach (var dataItem in dataItems)
        {
            // Resolve the table type for this data item via the semantic model.
            var tableType = GetDataItemTableType(dataItem);
            if (tableType is null)
                continue;

            // Match against the annotation's table name.
            if (!string.Equals(
                    Formatter.UnquoteIdentifier(tableType.Name),
                    annotation.TableName,
                    StringComparison.OrdinalIgnoreCase))
                continue;

            // Get fields from the table symbol, filtering out FlowFields, FlowFilters
            // and disabled fields.
            var tableFields = GetTableFieldsFromSymbol(tableType);

            var existingColumnSources = GetExistingColumnSources(dataItem);

            var missingFields = tableFields
                .Where(f =>
                    !existingColumnSources.Contains(f) &&
                    !annotation.Blacklist.Contains(f))
                .ToList();

            if (missingFields.Count == 0)
                continue;

            var newDataItem = AddColumns(dataItem, missingFields);
            result = result.ReplaceNode(dataItem, newDataItem);
        }

        return result;
    }

    /// <summary>
    /// Returns the table type symbol for a query data item by resolving it through
    /// the semantic model, so both workspace tables and package tables are supported.
    /// </summary>
    private ITypeSymbol? GetDataItemTableType(QueryDataItemSyntax dataItem)
    {
        var symbol = _model.GetDeclaredSymbol(dataItem);
        if (symbol is null)
            return null;

        // A query data item iterates over a table; its type IS the table type.
        var typeSymbol = symbol.GetTypeSymbol();
        if (typeSymbol is null || typeSymbol.Kind == SymbolKind.ErrorType)
            return null;

        return typeSymbol;
    }

    /// <summary>
    /// Enumerates all included field names from the given table type symbol.
    /// Excludes FlowField, FlowFilter and disabled fields.
    /// </summary>
    private static ImmutableList<string> GetTableFieldsFromSymbol(ITypeSymbol tableType)
    {
        return [.. tableType.GetMembers()
            .Where(IsIncludedField)
            .Select(m => Formatter.UnquoteIdentifier(m.Name))];
    }

    private static bool IsIncludedField(ISymbol member)
    {
        // Only table field members — cast to the AL-specific interface.
        if (member is not IFieldSymbol field)
            return false;

        // Exclude FlowField and FlowFilter
        if (field.FieldClass != FieldClassKind.Normal)
            return false;

        // Exclude system fields.
        if (field.Id >= 2000000000)
            return false;

        // There could be still system fields with lower IDs, like SystemRowVersion.
        if (field.Name.StartsWith("System", StringComparison.InvariantCultureIgnoreCase))
            return false;

        // Exclude BLOB fields, as they cannot be used as query columns.
        if (field.Type?.NavTypeKind == NavTypeKind.Blob)
            return false;

        // Exclude disabled fields. There definition exists, but they are not actually in the database -> can't be used in queries.
        if (field.Properties.Any(p => p.Name.EqualsOrdinalIgnoreCase("Enabled") && FalsyString(p.ValueText)))
            return false;

        return true;
    }

    private static HashSet<string> GetExistingColumnSources(QueryDataItemSyntax dataItem)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Only direct QueryColumnSyntax children (not those inside nested data items).
        foreach (var column in dataItem.ChildNodes().OfType<QueryColumnSyntax>())
        {
            result.Add(Formatter.UnquoteIdentifier(column.RelatedField.Identifier.Text));
        }

        return result;
    }

    private static QueryDataItemSyntax AddColumns(QueryDataItemSyntax dataItem, List<string> fieldNames)
    {
        var colmnsSyntax = fieldNames.Select(f =>
            SyntaxFactory.QueryColumn(MakeColumnIdentifier(f))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
            .WithRelatedField(SyntaxFactory.IdentifierName(Formatter.QuoteIdentifier(f)))
            .WithTrailingTrivia(SyntaxFactory.Linefeed)
        ).ToArray<QueryDataItemElementSyntax>();
        return dataItem.AddElements(colmnsSyntax);
    }

    /// <summary>
    /// Converts a field name (possibly with spaces, dots, hyphens) to a valid AL identifier.
    /// E.g. "Sell-to Customer No." → "SellToCustomerNo"
    /// </summary>
    public static string MakeColumnIdentifier(string fieldName)
    {
        var words = fieldName.Split(WordSeperator, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpper(w[0]) + (w.Length > 1 ? w[1..] : ""));

        var identifier = NonAlphaNumericRegex().Replace(string.Concat(words), "");

        if (char.IsDigit(identifier[0]))
            identifier = "F" + identifier;

        return identifier;
    }

    private QueryAnnotation? ParseAnnotation(QuerySyntax query)
    {
        var trivia = query.GetLeadingTrivia();

        string? tag = null;
        string? tableName = null;
        var blacklist = new List<string>();
        bool inAnnotation = false;

        foreach (var t in trivia)
        {
            var text = t.ToString();
            if (!text.TrimStart().StartsWith("//"))
                continue;

            var annotationMatch = AnnotationRegex().Match(text);
            if (annotationMatch.Success)
            {
                tag = annotationMatch.Groups[1].Value.Trim();
                tableName = Formatter.UnquoteIdentifier(annotationMatch.Groups[2].Value.Trim());
                inAnnotation = true;
                continue;
            }

            if (inAnnotation)
            {
                var blacklistMatch = BlacklistLineRegex().Match(text);
                if (blacklistMatch.Success)
                {
                    blacklist.Add(Formatter.UnquoteIdentifier(blacklistMatch.Groups[1].Value.Trim()));
                }
                else
                {
                    inAnnotation = false;
                }
            }
        }

        if (tag is null || tableName is null)
            return null;

        return new QueryAnnotation(
            tag,
            tableName,
            blacklist.ToImmutableHashSet(StringComparer.OrdinalIgnoreCase));
    }

    private bool IsTagActive(string tag)
    {
        return _activeTags.Count == 0 || _activeTags.Contains(tag);
    }

    private static bool FalsyString(string? s)
    {
        return "0".EqualsOrdinalIgnoreCase(s) || "no".EqualsOrdinalIgnoreCase(s) || "false".EqualsOrdinalIgnoreCase(s);
    }
}
