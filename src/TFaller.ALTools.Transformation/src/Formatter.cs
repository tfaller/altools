using Microsoft.Dynamics.Nav.CodeAnalysis;
using ALFormatter = Microsoft.Dynamics.Nav.CodeAnalysis.Workspaces.Formatting.Formatter;
using Microsoft.Dynamics.Nav.CodeAnalysis.Utilities;
using Microsoft.Dynamics.Nav.EditorServices.Protocol;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace TFaller.ALTools.Transformation;

public sealed partial class Formatter : IDisposable
{
    public static readonly ImmutableHashSet<string> Keywords = new HashSet<string>()
    {
        "begin",
        "end",
        "exit",
        "for",
        "key",
        "if",
        "then",
        "to",
        "var",
        "with"
    }.ToImmutableHashSet(StringComparer.InvariantCultureIgnoreCase);

    private readonly VsCodeWorkspace _workspace = new();

    /// <summary>
    /// Formats a given node to the common AL formatting style. Don't
    /// run this in parallel on multiples nodes. You could, its thread-safe.
    /// However, it won't be faster than running one after another, because
    /// the formater itself is highly parallelized. Parallel formating steals
    /// resources from the other running formaters and it evens out.
    /// </summary>
    /// <typeparam name="T">A type of SyntaxNode</typeparam>
    /// <param name="node">A node that sould be formated</param>
    /// <returns>
    /// The formated SyntaxNode. If it already was formatted,
    /// the same instance is returned.
    /// </returns>
    public T Format<T>(T node) where T : SyntaxNode
    {
        return (T)ALFormatter.Format(node, _workspace);
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    [GeneratedRegex("^[a-z_][a-z0-9_]*$", RegexOptions.IgnoreCase, 1000)]
    public static partial Regex IdentifierRegex();

    public static string CombineIdentifiers(params string[] identifiers)
    {
        var combined = identifiers.Select(i => i.Trim('"').ToPascalCase()).Join("");
        return QuoteIdentifier(combined);
    }

    public static string QuoteIdentifier(string identifier)
    {
        if (IdentifierRegex().IsMatch(identifier))
            return identifier;

        return "\"" + identifier + "\"";
    }

    public static string UnquoteIdentifier(string identifier)
    {
        if (identifier.StartsWith('"') && identifier.EndsWith('"'))
            return identifier[1..^1];

        return identifier;
    }
}