using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.Syntax;
using TFaller.ALTools.Transformation.Rewriter;

namespace TFaller.ALTools.Transformation.Transformer.CommentRule;

/// <summary>
/// Rewriter that keeps or removes conditional code blocks based on @altools:transform comments.
///
/// Example:
/// // @altools:transform:cloud:cond:if:HasFeature
/// Foo();
/// // @altools:transform:cloud:cond:end
///
/// If the flag "HasFeature" is true, the block is kept, otherwise it is removed.
/// The two comment lines are always removed. Blocks can be nested.
///
/// Syntax:
///   @altools:transform:TAG:cond:if:FLAG
///   @altools:transform:TAG:cond:end
///
/// - Directives of tags that are not active are left untouched (like in the other comment rules).
/// - An undefined flag is an error, as is a missing "end" or "if".
///
/// The directives are line based and can be placed anywhere (statements, members, variables, ...).
/// As the block boundaries do not need to match the syntax tree, the rewriter works on the text
/// and parses the result again.
/// </summary>
public partial class TransformCond : IReuseableRewriter
{
    [GeneratedRegex(
        @"^\s*//\s*@altools:transform:(?<tag>[^:]+):cond:(?:if:(?<flag>.+?)|(?<end>end))\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    public static partial Regex DirectiveRegex();

    private readonly HashSet<string> _activeTags;
    private readonly Dictionary<string, bool> _flags;

    public TransformCond(HashSet<string>? activeTags = null, IDictionary<string, bool>? flags = null)
    {
        _activeTags = activeTags ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _flags = new Dictionary<string, bool>(flags ?? new Dictionary<string, bool>(), StringComparer.OrdinalIgnoreCase);
    }

    public IReuseableRewriter Clone() => new TransformCond(_activeTags, _flags);

    public IRewriterContext EmptyContext => new RewriterContext();

    public bool RerunUntilNoChanges => false;

    public SyntaxNode Rewrite(SyntaxNode node, ref IRewriterContext context)
    {
        var text = node.ToFullString();
        if (!text.Contains("@altools:transform:", StringComparison.OrdinalIgnoreCase))
        {
            return node;
        }

        var result = Process(text, out var changed);
        if (!changed)
        {
            return node;
        }

        return SyntaxTree.ParseObjectText(result, node.SyntaxTree.FilePath, Encoding.UTF8, node.SyntaxTree.Options).GetRoot();
    }

    /// <summary>
    /// Removes the directive lines and the blocks of false flags from the text.
    /// </summary>
    internal string Process(string text, out bool changed)
    {
        var sb = new StringBuilder(text.Length);
        // one entry per open "if": whether its block is kept (and the line number for error messages)
        var open = new Stack<(bool Keep, int Line, string Tag)>();
        var removedLines = 0;
        var lineNo = 0;
        var pos = 0;

        while (pos < text.Length)
        {
            var eol = text.IndexOf('\n', pos);
            var next = eol < 0 ? text.Length : eol + 1;
            var line = text.AsSpan(pos, next - pos);
            lineNo++;

            var keep = true;
            foreach (var frame in open)
            {
                if (!frame.Keep)
                {
                    keep = false;
                    break;
                }
            }

            var match = DirectiveRegex().Match(line.ToString());
            var tag = match.Success ? match.Groups["tag"].Value.Trim() : null;
            if (match.Success && (_activeTags.Count == 0 || _activeTags.Contains(tag!)))
            {
                if (match.Groups["end"].Success)
                {
                    if (open.Count == 0 || !string.Equals(open.Peek().Tag, tag, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException($"Line {lineNo}: @altools:transform:{tag}:cond:end without matching if");
                    }

                    open.Pop();
                }
                else
                {
                    var flag = match.Groups["flag"].Value.Trim();
                    if (!_flags.TryGetValue(flag, out var value))
                    {
                        throw new InvalidOperationException($"Line {lineNo}: flag '{flag}' is not defined in the configuration");
                    }

                    open.Push((value, lineNo, tag!));
                }

                removedLines++;
            }
            else if (keep)
            {
                sb.Append(line);
            }
            else
            {
                removedLines++;
            }

            pos = next;
        }

        if (open.Count > 0)
        {
            throw new InvalidOperationException($"Line {open.Peek().Line}: @altools:transform:{open.Peek().Tag}:cond:if without matching end");
        }

        changed = removedLines > 0;
        return changed ? sb.ToString() : text;
    }
}
