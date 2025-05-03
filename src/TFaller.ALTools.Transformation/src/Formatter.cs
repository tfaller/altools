using Microsoft.Dynamics.Nav.CodeAnalysis;
using ALFormatter = Microsoft.Dynamics.Nav.CodeAnalysis.Workspaces.Formatting.Formatter;
using Microsoft.Dynamics.Nav.EditorServices.Protocol;
using System;

namespace TFaller.ALTools.Transformation;

public sealed class Formatter : IDisposable
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
}