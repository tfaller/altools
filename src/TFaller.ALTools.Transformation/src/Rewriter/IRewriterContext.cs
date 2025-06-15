using System.Collections.Immutable;
using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.Syntax;

namespace TFaller.ALTools.Transformation.Rewriter;

/// <summary>
/// Context interface for rewriters that need information for rewriting.
/// All data held directly or nested in this context should be immutable to ensure thread-safety.
/// </summary>
public interface IRewriterContext
{
    /// <summary>
    /// Semantic model of the original syntax tree that is being rewritten.
    /// </summary>
    public SemanticModel Model { get; }

    /// <summary>
    /// Creates a context with the given semantic model.
    /// </summary>
    public IRewriterContext WithModel(SemanticModel model);

    /// <summary>
    /// Contexts of processed syntax trees
    /// </summary>
    public ImmutableDictionary<SyntaxTree, IRewriterContext> Contexts { get; }

    /// <summary>
    /// Creates a context with the given contexts.
    /// </summary>
    public IRewriterContext WithContexts(ImmutableDictionary<SyntaxTree, IRewriterContext> contexts);

    /// <summary>
    /// A set of syntax trees that this rewriter depends on.
    /// The trees must be the original trees, not the rewritten ones.
    /// </summary>
    public ImmutableHashSet<SyntaxTree> Dependencies { get; }
}