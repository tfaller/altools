using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.Syntax;
using TFaller.ALTools.Transformation.Rewriter;

namespace TFaller.ALTools.Transformation.Transformer.CommentRule;

using Context = RewriterContextWithState<TransformParam.State>;

/// <summary>
/// Rewriter that transforms procedure/trigger parameters based on @altools:transform comments
/// placed above the signature. Multiple comments per signature are supported.
/// 
/// Example:
/// // @altools:transform:cloud:param:OldParam:NewParam: Record "New Sales Line"
/// // @altools:transform:cloud:param:Other: Text[100]
/// procedure Foo(OldParam: Record "Sales Line"; var Other: Text)
/// 
/// Syntax: @altools:transform:TAG:param:OldName[:NewName]: Type
/// - If NewName is omitted, the name is kept.
/// - OldName is just the parameter name (no var keyword).
/// - Prefixing NewName with "var " makes the parameter a var parameter, omitting it removes var.
///   If NewName is omitted, var is kept as declared.
/// </summary>
public partial class TransformParam : SyntaxRewriter, IReuseableRewriter
{
    private const string Ident = "(?:\"[^\"]*\"|\\w+)";

    [GeneratedRegex(
        @"@altools:transform:(?<tag>[^:]+):param:\s*(?<old>" + Ident + @")\s*:(?:\s*(?<new>(?:var\s+)?" + Ident + @")\s*:)?\s*(?<type>.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    public static partial Regex TransformCommentRegex();

    private Context _context = null!;
    private SemanticModel _model = null!;
    private State _state = null!;
    private readonly HashSet<string> _activeTags;

    public TransformParam(HashSet<string>? activeTags = null)
    {
        _activeTags = activeTags ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public class State
    {
        /// <summary>
        /// Maps original parameter symbols to their transformation
        /// </summary>
        public Dictionary<ISymbol, TransformInfo> ParameterTransforms { get; } = [];
    }

    public record TransformInfo(string NewName, bool? NewIsVar, string NewTypeText);

    private record ParsedComment(string OldName, TransformInfo Info);

    public SyntaxNode Rewrite(SyntaxNode node, ref IRewriterContext context)
    {
        _context = (Context)context;
        _model = context.Model;
        _state = _context.State ?? new State();

        var rewritten = Visit(node);

        context = _context.WithState(_state);

        return rewritten;
    }

    public IReuseableRewriter Clone()
    {
        return new TransformParam(_activeTags);
    }

    public IRewriterContext EmptyContext => new Context();

    public bool RerunUntilNoChanges => false;

    public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (!RegisterTransforms(node, node.ParameterList, out var predicate))
        {
            return base.VisitMethodDeclaration(node);
        }

        var result = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!;
        result = result.WithLeadingTrivia(TransformVar.RemoveTransformComment(result.GetLeadingTrivia(), predicate));

        var tableChanges = GetTableChanges(node, result);
        return tableChanges.Count == 0 ? result : UpdateEventSubscribers(result, tableChanges);
    }

    /// <summary>
    /// Collects old table name -> new table name for all transformed Record parameters whose table changes.
    /// </summary>
    private static Dictionary<string, string> GetTableChanges(MethodDeclarationSyntax oldNode, MethodDeclarationSyntax newNode)
    {
        var changes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = oldNode.ParameterList.Parameters.Count - 1; i >= 0; i--)
        {
            var oldTable = Formatter.UnquoteIdentifier(GetRecordParameterTableName(oldNode.ParameterList.Parameters[i]) ?? "+");
            var newTable = Formatter.UnquoteIdentifier(GetRecordParameterTableName(newNode.ParameterList.Parameters[i]) ?? "-");

            if (!oldTable.EqualsOrdinalIgnoreCase(newTable))
            {
                changes.TryAdd(oldTable, newTable);
            }
        }

        return changes;
    }

    private static string? GetRecordParameterTableName(ParameterSyntax parameter)
    {
        if (parameter.Type is not RecordTypeReferenceSyntax recRef ||
            recRef.DataType is not SubtypedDataTypeSyntax subtyped ||
            subtyped.Subtype is not ObjectNameOrIdSyntax typeIdent ||
            typeIdent.Identifier is not IdentifierNameSyntax id)
        {
            return null;
        }

        return id.Identifier.ValueText;
    }

    /// <summary>
    /// Adjusts [EventSubscriber(ObjectType::Table, Database::OldTable, ...)] when the table of the Record parameter changed.
    /// </summary>
    private static MethodDeclarationSyntax UpdateEventSubscribers(MethodDeclarationSyntax method, Dictionary<string, string> tableChanges)
    {
        foreach (var attribute in method.Attributes.ToList())
        {
            if (!"EventSubscriber".EqualsOrdinalIgnoreCase(attribute.Name.Identifier.ValueText))
            {
                continue;
            }

            var arg = attribute.ArgumentList.Arguments[1];
            if (arg is not OptionAccessAttributeArgumentSyntax optionAccessArg ||
                optionAccessArg.OptionAccess.Expression is not IdentifierNameSyntax optionBaseIdent ||
                !"Database".EqualsOrdinalIgnoreCase(optionBaseIdent.Identifier.ValueText))
            {
                continue;
            }

            if (!tableChanges.TryGetValue(Formatter.UnquoteIdentifier(optionAccessArg.OptionAccess.Name.ToString()), out var newTable))
            {
                continue;
            }

            var updated = SyntaxFactory.OptionAccessAttributeArgument(SyntaxFactory.OptionAccessExpression(
                SyntaxFactory.IdentifierName("Database"),
                SyntaxFactory.Token(SyntaxKind.ColonColonToken),
                SyntaxFactory.IdentifierName(Formatter.QuoteIdentifier(newTable))
            ));

            method = method.ReplaceNode(method.Attributes.First(a => a.IsEquivalentTo(attribute)).ArgumentList.Arguments[1], updated);
        }

        return method;
    }

    public override SyntaxNode VisitTriggerDeclaration(TriggerDeclarationSyntax node)
    {
        if (!RegisterTransforms(node, node.ParameterList, out var predicate))
        {
            return base.VisitTriggerDeclaration(node);
        }

        var result = (TriggerDeclarationSyntax)base.VisitTriggerDeclaration(node)!;
        return result.WithLeadingTrivia(TransformVar.RemoveTransformComment(result.GetLeadingTrivia(), predicate));
    }

    public override SyntaxNode VisitParameter(ParameterSyntax node)
    {
        var symbol = _model.GetDeclaredSymbol(node);
        if (symbol is null || !_state.ParameterTransforms.TryGetValue(symbol, out var info))
        {
            return base.VisitParameter(node);
        }

        var newName = SyntaxFactory.IdentifierName(info.NewName).WithTriviaFrom(node.Name);
        var newType = TransformVar.ParseTypeReference(info.NewTypeText).WithTriviaFrom(node.Type);

        var result = node.WithName(newName).WithType(newType);

        var hasVar = !node.VarKeyword.IsKind(SyntaxKind.None);
        var newIsVar = info.NewIsVar ?? hasVar;
        if (hasVar && !newIsVar)
        {
            // Hand the keyword's leading trivia to the name so spacing stays intact
            result = result
                .WithVarKeyword(default)
                .WithName(result.Name.WithLeadingTrivia(node.VarKeyword.LeadingTrivia));
        }
        else if (!hasVar && newIsVar)
        {
            result = result
                .WithVarKeyword(SyntaxFactory.Token(SyntaxKind.VarKeyword)
                    .WithLeadingTrivia(node.Name.GetLeadingTrivia())
                    .WithTrailingTrivia(SyntaxFactory.Space))
                .WithName(result.Name.WithLeadingTrivia(SyntaxFactory.TriviaList()));
        }

        return result;
    }

    public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
    {
        var symbol = _model.GetSymbolInfo(node).Symbol;

        if (symbol != null && _state.ParameterTransforms.TryGetValue(symbol, out var info))
        {
            return node.WithIdentifier(SyntaxFactory.Identifier(info.NewName).WithTriviaFrom(node.Identifier));
        }

        return base.VisitIdentifierName(node);
    }

    /// <summary>
    /// Parses the active transform comments of a signature and registers the parameter symbols.
    /// Returns false when there is nothing to transform.
    /// </summary>
    private bool RegisterTransforms(SyntaxNode node, ParameterListSyntax? parameterList, out Func<string, bool> removePredicate)
    {
        var comments = new List<ParsedComment>();
        foreach (var trivia in node.GetLeadingTrivia())
        {
            var text = trivia.ToString();
            if (!text.TrimStart().StartsWith("//"))
            {
                continue;
            }

            var comment = ParseComment(text);
            if (comment is not null)
            {
                comments.Add(comment);
            }
        }

        removePredicate = text => ParseComment(text) is not null;

        if (comments.Count == 0)
        {
            return false;
        }

        var parameters = parameterList?.Parameters.ToList() ?? [];

        foreach (var comment in comments)
        {
            var param = parameters.FirstOrDefault(p =>
                string.Equals(p.Name.Identifier.ValueText, comment.OldName, StringComparison.OrdinalIgnoreCase)) ??
                throw new InvalidOperationException($"Could not find parameter '{comment.OldName}' for @altools:transform comment");

            var symbol = _model.GetDeclaredSymbol(param) ??
                throw new InvalidOperationException($"Could not get symbol for parameter '{comment.OldName}'");

            _state.ParameterTransforms[symbol] = comment.Info;
        }

        return true;
    }

    private ParsedComment? ParseComment(string triviaText)
    {
        var match = TransformCommentRegex().Match(triviaText);
        if (!match.Success)
        {
            return null;
        }

        var tag = match.Groups["tag"].Value.Trim();
        if (_activeTags.Count != 0 && !_activeTags.Contains(tag))
        {
            return null;
        }

        var oldName = match.Groups["old"].Value;
        var (newName, newIsVar) = match.Groups["new"].Success
            ? SplitVar(match.Groups["new"].Value)
            : (oldName, (bool?)null);

        return new ParsedComment(
            oldName.Trim('"'),
            new TransformInfo(newName, newIsVar, match.Groups["type"].Value.Trim()));
    }

    /// <summary>
    /// Splits an optional "var " prefix from a name. The returned name keeps its quotes.
    /// </summary>
    private static (string Name, bool? IsVar) SplitVar(string text)
    {
        text = text.Trim();
        var isVar = VarPrefixRegex().IsMatch(text);
        if (isVar)
        {
            text = VarPrefixRegex().Replace(text, "");
        }

        return (text, isVar);
    }

    [GeneratedRegex(@"^var\s+", RegexOptions.IgnoreCase)]
    private static partial Regex VarPrefixRegex();
}
