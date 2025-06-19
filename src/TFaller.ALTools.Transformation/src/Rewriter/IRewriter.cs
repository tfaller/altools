using Microsoft.Dynamics.Nav.CodeAnalysis;

namespace TFaller.ALTools.Transformation.Rewriter;

/// <summary>
/// A rewriter that can be used to transform syntax trees.
/// </summary>
public interface IRewriter
{
    /// <summary>
    /// An empty context that can be used for rewriting.
    /// </summary>
    public IRewriterContext EmptyContext { get; }

    /// <summary>
    /// It is possible that a rewriter can't perform all transformations in one go,
    /// or it technically could, but it would be too complex. Like one transformation depends on another.
    /// If this is set, the rewriter should be run multiple times until no changes are made.
    /// A rerun is here ment as rewriting all syntax trees, not just a given syntax tree.
    /// Just one syntax tree can be done with dependencies.
    /// The set value must be const and never change. Either the rewriter supports/wants reruns or not.
    /// It must not change because of some state, like previous processed syntax trees.
    /// </summary>
    public bool RerunUntilNoChanges { get; }

    /// <summary>
    /// Rewrites the given syntax node using the provided semantic model.
    /// No quarantee is made that the rewriter is thread-safe. Use specialized rewriters for that purpose.
    /// </summary>
    public SyntaxNode Rewrite(SyntaxNode node, ref IRewriterContext metadata);
}