using Microsoft.Dynamics.Nav.CodeAnalysis;

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
}