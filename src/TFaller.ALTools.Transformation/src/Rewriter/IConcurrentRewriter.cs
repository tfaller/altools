using Microsoft.Dynamics.Nav.CodeAnalysis;

namespace TFaller.ALTools.Transformation.Rewriter;

/// <summary>
/// A rewriter that can be executed concurrently.
/// </summary>
public interface IConcurrentRewriter : IRewriter
{
    /// <summary>
    /// Rewrites the given syntax node using the provided semantic model.
    /// This method will be called concurrently, so it must be thread-safe.
    /// </summary>
    public new SyntaxNode Rewrite(SyntaxNode node, ref IRewriterContext metadata);
}