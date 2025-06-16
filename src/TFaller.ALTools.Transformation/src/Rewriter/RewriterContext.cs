using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.Syntax;

namespace TFaller.ALTools.Transformation.Rewriter;

using ContextsDictionary = ImmutableDictionary<SyntaxTree, IRewriterContext>;

public class RewriterContext : IRewriterContext
{
    public SemanticModel Model { protected init; get; } = null!;
    public ImmutableHashSet<SyntaxTree> Dependencies { protected init; get; } = [];
    public ContextsDictionary Contexts { protected init; get; } = ContextsDictionary.Empty;

    public IRewriterContext WithModel(SemanticModel model)
    {
        return Update(model, Dependencies, Contexts);
    }

    public IRewriterContext WithContexts(ContextsDictionary contexts)
    {
        return Update(Model, Dependencies, contexts);
    }

    public RewriterContext AddDependencies(IEnumerable<SyntaxTree> dependencies)
    {
        return Update(Model, Dependencies.Union(dependencies), Contexts);
    }

    public RewriterContext AddDependencies(params SyntaxTree[] dependencies)
    {
        return Update(Model, Dependencies.Union(dependencies), Contexts);
    }

    public RewriterContext WithDependencies(IEnumerable<SyntaxTree> dependencies)
    {
        return Update(Model, [.. dependencies], Contexts);
    }

    public RewriterContext WithDependencies(params SyntaxTree[] dependencies)
    {
        return Update(Model, [.. dependencies], Contexts);
    }

    public virtual RewriterContext Update(
        SemanticModel model,
        ImmutableHashSet<SyntaxTree> dependencies,
        ContextsDictionary contexts
    )
    {
        AssertContextType<RewriterContext>(contexts);

        return new RewriterContext
        {
            Model = model,
            Dependencies = dependencies,
            Contexts = contexts
        };
    }

    public bool TryGetContext(SyntaxTree tree, [NotNullWhen(true)] out RewriterContext? context)
    {
        return (Contexts.TryGetValue(tree, out var ctx) && (context = (RewriterContext)ctx) is not null)
            || (context = null) is not null;
    }

    [Conditional("DEBUG")]
    protected virtual void AssertContextType<T>(ContextsDictionary contexts)
    {
        Debug.Assert(contexts.Values.All(c => c is T), $"All contexts must be of type {typeof(T)}");
    }
}