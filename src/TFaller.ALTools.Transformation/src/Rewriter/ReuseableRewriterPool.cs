using System.Collections.Generic;
using Microsoft.Dynamics.Nav.CodeAnalysis;

namespace TFaller.ALTools.Transformation.Rewriter;

/// <summary>
/// Pools reuseable rewriters so that concurrent rewrites can be performed without creating new instances each time.
/// </summary>
/// <param name="baseRewriter">The rewriter which gets used by this pool</param>
public class ReuseableRewriterPool(IReuseableRewriter baseRewriter) : IConcurrentRewriter
{
    private readonly Stack<IReuseableRewriter> _pool = new();

    public SyntaxNode Rewrite(SyntaxNode node, SemanticModel model)
    {
        IReuseableRewriter rewriter;

        lock (_pool)
        {
            rewriter = _pool.Count > 0 ? _pool.Pop() : baseRewriter.Clone();
        }

        try
        {
            return rewriter.Rewrite(node, model);
        }
        finally
        {
            lock (_pool)
            {
                _pool.Push(rewriter);
            }
        }
    }
}