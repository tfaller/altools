using Microsoft.Dynamics.Nav.CodeAnalysis;

namespace TFaller.ALTools.Transformation.Rewriter;

public class RewriterContext : IRewriterContext
{
    public SemanticModel Model { private init; get; } = null!;

    public IRewriterContext WithModel(SemanticModel model)
    {
        return new RewriterContext
        {
            Model = model
        };
    }
}