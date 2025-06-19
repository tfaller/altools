using Microsoft.Dynamics.Nav.CodeAnalysis;

namespace TFaller.ALTools.Transformation.Rewriter;

/// <summary>
/// A rewriter that can be reused for multiple rewrites without creating new instances each time.
/// By convention, it is not thread-safe, so it must not be used concurrently. Use IConcurrentRewriter for that purpose.
/// </summary>
public interface IReuseableRewriter : IRewriter
{
    /// <summary>
    /// Rewrites the given syntax node using the provided semantic model.
    /// This method should not be called concurrently, so it must not be thread-safe.
    /// Use Clone() to create a new instance for each concurrent call if needed.
    /// </summary>
    /// <param name="node">Node that sould be rewritten</param>
    /// <param name="context">Context to use for rewriting</param>
    /// <returns>Rewritten node</returns>
    public new SyntaxNode Rewrite(SyntaxNode node, ref IRewriterContext context);

    /// <summary>
    /// A clone of itself, so that a (parallel) Rewrite() on the clone works independently of the original.
    /// It must not be an exact copy, it just must work as expected.
    /// </summary>
    /// <returns>A clone of the rewriter</returns>
    public IReuseableRewriter Clone();
}