using Microsoft.Dynamics.Nav.CodeAnalysis;
using ALFormatter = Microsoft.Dynamics.Nav.CodeAnalysis.Workspaces.Formatting.Formatter;
using Microsoft.Dynamics.Nav.CodeAnalysis.Utilities;
using Microsoft.Dynamics.Nav.EditorServices.Protocol;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace TFaller.ALTools.Transformation;

public sealed partial class Formatter : IDisposable
{
    private readonly VsCodeWorkspace _workspace = new();

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
}