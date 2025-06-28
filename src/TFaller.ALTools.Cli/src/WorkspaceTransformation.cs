namespace TFaller.ALTools.Cli;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TFaller.ALTools.Transformation;
using TFaller.ALTools.Transformation.Rewriter;

internal static class WorkspaceTransformation
{
    public static readonly Dictionary<string, Lazy<IConcurrentRewriter>> Rewriters = new(StringComparer.OrdinalIgnoreCase)
    {
        ["complex-return-transpiler"] = new(() => Pool(new ComplexReturnTranspiler())),
        ["complex-return-uplifter"] = new(() => Pool(new ComplexReturnUplifter())),
    };

    public static async Task Transform(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: workspace-transform <workspace-path> <rewriter>[,<rewriter>]*");
            Environment.Exit(1);
        }

        var workspacePath = args[0];
        var rewritersNames = args[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var rewriters = new List<IConcurrentRewriter>();

        foreach (var rewriterName in rewritersNames)
        {
            if (!Rewriters.TryGetValue(rewriterName, out var lazyRewriter))
            {
                Console.Error.WriteLine($"Unknown rewriter: {rewriterName}. Available rewriters: \n{string.Join("\n", Rewriters.Keys)}");
                Environment.Exit(1);
            }

            rewriters.Add(lazyRewriter.Value);
        }

        var start = DateTime.Now;

        var rewriter = new WorkspaceRewriter(rewriters, null!);
        await rewriter.Rewrite(workspacePath);

        Console.WriteLine($"Transformed workspace in {DateTime.Now - start}");
    }

    private static ReuseableRewriterPool Pool(IReuseableRewriter rewriter) => new(rewriter);
}