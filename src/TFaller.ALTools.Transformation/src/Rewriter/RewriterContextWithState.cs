using System.Collections.Immutable;
using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.Syntax;

namespace TFaller.ALTools.Transformation.Rewriter;

/// <summary>
/// A rewriter context that holdes a custom state.
/// </summary>
/// <typeparam name="T">Type of the state</typeparam>
public class RewriterContextWithState<T> : RewriterContext
{
    public T State { protected init; get; } = default!;

    public RewriterContextWithState<T> WithState(T state)
    {
        return Update(Model, Dependencies, Contexts, state);
    }

    public override RewriterContext Update(
        SemanticModel model,
        ImmutableHashSet<SyntaxTree> dependencies,
        ImmutableDictionary<SyntaxTree, IRewriterContext> contexts)
    {
        return Update(model, dependencies, contexts, State);
    }

    public RewriterContextWithState<T> Update(
        SemanticModel model,
        ImmutableHashSet<SyntaxTree> dependencies,
        ImmutableDictionary<SyntaxTree, IRewriterContext> contexts,
        T state
    )
    {
        return new RewriterContextWithState<T>
        {
            Model = model,
            Dependencies = dependencies,
            Contexts = contexts,
            State = state
        };
    }
}