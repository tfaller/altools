using Microsoft.Dynamics.Nav.CodeAnalysis;
using System;
using System.Collections.Generic;
using TFaller.ALTools.Transformation.Transformer.CommentRule;

namespace TFaller.ALTools.Transformation.Tests;

public class TransformCondTests
{
    private const string Input = """
        codeunit 1 Test
        {
            procedure Do()
            begin
                A();
                // @altools:transform:cloud:cond:if:Feature
                B();
                // @altools:transform:cloud:cond:if:Other
                C();
                // @altools:transform:cloud:cond:end
                D();
                // @altools:transform:cloud:cond:end
                E();
            end;
        }
        """;

    private static string Run(string input, string? tags, Dictionary<string, bool> flags)
    {
        var compilationUnit = SyntaxFactory.ParseCompilationUnit(input);
        var compilation = Compilation.Create("temp").AddSyntaxTrees(compilationUnit.SyntaxTree);
        var model = compilation.GetSemanticModel(compilationUnit.SyntaxTree);

        var activeTags = tags is null ? null : new HashSet<string>([tags], StringComparer.OrdinalIgnoreCase);
        var rewriter = new TransformCond(activeTags, flags);
        var context = rewriter.EmptyContext.WithModel(model);
        return rewriter.Rewrite(compilationUnit, ref context).ToFullString();
    }

    [Theory]
    [InlineData(true, true, "A(); B(); C(); D(); E();")]
    [InlineData(true, false, "A(); B(); D(); E();")]
    [InlineData(false, true, "A(); E();")]
    [InlineData(false, false, "A(); E();")]
    public void NestedFlags(bool feature, bool other, string calls)
    {
        var result = Run(Input, "cloud", new() { ["Feature"] = feature, ["Other"] = other });

        Assert.DoesNotContain("@altools", result);
        Assert.Equal(
            $"codeunit1Test{{procedureDo()begin{calls}end;}}".Replace(" ", ""),
            string.Concat(result.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)));
    }

    [Fact]
    public void InactiveTagIsUntouched()
    {
        var result = Run(Input, "onprem", []);
        Assert.Equal(Input, result);
    }

    [Fact]
    public void UndefinedFlagThrows()
    {
        Assert.Throws<InvalidOperationException>(() => Run(Input, "cloud", new() { ["Feature"] = true }));
    }

    [Fact]
    public void MissingEndThrows()
    {
        var input = Input.Replace("// @altools:transform:cloud:cond:end", "");
        Assert.Throws<InvalidOperationException>(() => Run(input, "cloud", new() { ["Feature"] = true, ["Other"] = true }));
    }
}
