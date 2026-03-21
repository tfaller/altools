using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.Syntax;
using TFaller.ALTools.Transformation.Rewriter;

namespace TFaller.ALTools.Transformation.Transformer.CommentRule;

using Context = RewriterContextWithState<TransformVar.State>;

/// <summary>
/// Rewriter that transforms variable declarations based on @altools:transform comments.
/// 
/// Example: 
/// // @altools:transform:cloud:var:NewSalesLine: Record "New Sales Line"
/// SalesLine: Record "Sales Line";
/// 
/// This will rename SalesLine to NewSalesLine and change its type to Record "New Sales Line"
/// if the "cloud" tag is active.
/// </summary>
public partial class TransformVar : SyntaxRewriter, IReuseableRewriter
{
    [GeneratedRegex(@"@altools:transform:([^:]+):var:([^:]+):\s*(.+)$", RegexOptions.Compiled)]
    public static partial Regex TransformCommentRegex();

    private Context _context = null!;
    private SemanticModel _model = null!;
    private State _state = null!;
    private readonly HashSet<string> _activeTags;

    public TransformVar(HashSet<string>? activeTags = null)
    {
        _activeTags = activeTags ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public class State
    {
        /// <summary>
        /// Maps original variable symbols to their new names and types
        /// </summary>
        public Dictionary<ISymbol, TransformInfo> VariableTransforms { get; } = [];
    }

    public record TransformInfo(string NewName, string NewTypeText);

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
        return new TransformVar(_activeTags);
    }

    public IRewriterContext EmptyContext => new Context();

    public bool RerunUntilNoChanges => false;

    public override SyntaxNode VisitVariableDeclaration(VariableDeclarationSyntax node)
    {
        // Check if this variable has a @altools:transform comment
        var transformInfo = ParseTransformComment(node);
        if (transformInfo is null)
        {
            return node;
        }

        // Get the symbol for this variable
        var symbol = _model.GetDeclaredSymbol(node) ??
            throw new InvalidOperationException("Could not get symbol for variable declaration");

        // Store the transformation info
        _state.VariableTransforms[symbol] = transformInfo;

        // Transform the variable declaration while preserving trivia (except the transform comment)
        var newName = SyntaxFactory.IdentifierName(transformInfo.NewName)
            .WithTriviaFrom(node.Name);

        var newType = ParseTypeReference(transformInfo.NewTypeText)
            .WithTriviaFrom(node.Type);

        // Remove the @altools:transform comment from leading trivia
        var leadingTrivia = node.GetLeadingTrivia();
        var newLeadingTrivia = RemoveTransformComment(leadingTrivia);

        var newNode = node
            .WithName(newName)
            .WithType(newType)
            .WithLeadingTrivia(newLeadingTrivia);

        return newNode;
    }

    public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
    {
        // Check if this identifier refers to a transformed variable
        var symbol = _model.GetSymbolInfo(node).Symbol;

        if (symbol != null && _state.VariableTransforms.TryGetValue(symbol, out var transformInfo))
        {
            // Rename the identifier
            return node.WithIdentifier(SyntaxFactory.Identifier(transformInfo.NewName).WithTriviaFrom(node.Identifier));
        }

        return base.VisitIdentifierName(node);
    }

    /// <summary>
    /// Parses the @altools:transform comment from the leading trivia of a variable declaration
    /// </summary>
    private TransformInfo? ParseTransformComment(VariableDeclarationSyntax node)
    {
        // Get leading trivia from the node
        var leadingTrivia = node.GetLeadingTrivia();

        // Look through trivia for comments
        foreach (var trivia in leadingTrivia)
        {
            // Check if this is a comment by checking the text
            var triviaText = trivia.ToString();
            if (triviaText.TrimStart().StartsWith("//"))
            {
                var match = TransformCommentRegex().Match(triviaText);

                if (match.Success)
                {
                    var tag = match.Groups[1].Value.Trim();
                    var newName = match.Groups[2].Value.Trim();
                    var newType = match.Groups[3].Value.Trim();

                    // Check if this tag is active
                    if (_activeTags.Count == 0 || _activeTags.Contains(tag))
                    {
                        return new TransformInfo(newName, newType);
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Removes the @altools:transform comment from the trivia list while preserving other trivia
    /// </summary>
    private static SyntaxTriviaList RemoveTransformComment(SyntaxTriviaList trivia)
    {
        var newTrivia = new List<SyntaxTrivia>();

        for (int i = 0; i < trivia.Count; i++)
        {
            var t = trivia[i];
            var triviaText = t.ToString();

            // Only skip trivia that contains the @altools:transform directive
            if (triviaText.TrimStart().StartsWith("//") && triviaText.Contains("@altools:transform:"))
            {
                // Remove previous whitespace trivia if it only contains whitespace to avoid leaving extra blank lines.
                if (newTrivia.Count > 0 &&
                    newTrivia[^1].IsKind(SyntaxKind.WhiteSpaceTrivia) &&
                    string.IsNullOrWhiteSpace(newTrivia[^1].ToString()))
                {
                    newTrivia.RemoveAt(newTrivia.Count - 1);
                }

                // also skip the next trivia if it's an end of line to avoid leaving blank lines
                if (i + 1 < trivia.Count && trivia[i + 1].IsKind(SyntaxKind.EndOfLineTrivia))
                {
                    i++;
                }

                // Skip this trivia (the @altools:transform comment)
                continue;
            }

            // Keep all other trivia
            newTrivia.Add(t);
        }

        return SyntaxFactory.TriviaList(newTrivia);
    }

    /// <summary>
    /// Parses a type reference string into a TypeReferenceSyntax
    /// </summary>
    private static TypeReferenceBaseSyntax ParseTypeReference(string typeText)
    {
        // Parse the type text as AL code
        // Try to parse it as a complete codeunit with variable declaration and extract the type
        var tempCode = $"codeunit 1 Temp {{ var temp: {typeText}; }}";
        var tempTree = SyntaxTree.ParseObjectText(tempCode, "", Encoding.UTF8, ParseOptions.Default);
        var root = tempTree.GetRoot();

        // Find the variable declaration and extract its type
        var varDecl = root.DescendantNodes().OfType<VariableDeclarationSyntax>().FirstOrDefault();
        if (varDecl?.Type != null)
        {
            // Return the type whether it's SimpleTypeReferenceSyntax, RecordTypeReferenceSyntax, or other
            return varDecl.Type;
        }

        // Fallback: throw an exception as we cannot parse the type
        var diagnosticsText = string.Join("; ", tempTree.GetDiagnostics().Select(d => d.GetMessage()));
        throw new InvalidOperationException($"Could not parse type reference '{typeText}'. Diagnostics: {diagnosticsText}");
    }
}
