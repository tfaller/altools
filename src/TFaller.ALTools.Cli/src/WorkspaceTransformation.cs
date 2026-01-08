namespace TFaller.ALTools.Cli;

using System;
using System.Collections.Generic;
using System.IO;
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
            Console.Error.WriteLine("       workspace-transform <workspace-path> file-name-normalizer");
            Environment.Exit(1);
        }

        var workspacePath = args[0];
        var transformName = args[1];

        if (string.Equals(transformName, "file-name-normalizer", StringComparison.OrdinalIgnoreCase))
        {
            await RunFileNameNormalizer(workspacePath);
            return;
        }

        var rewritersNames = transformName.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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

    private static async Task RunFileNameNormalizer(string workspacePath)
    {
        var start = DateTime.Now;

        var fileRenames = await FileNameNormalizer.AnalyzeWorkspace(workspacePath, null!);
        if (fileRenames.Count > 0)
        {
            Console.WriteLine($"Renaming {fileRenames.Count} files...");
            foreach (var rename in fileRenames)
            {
                Console.WriteLine($"  {Path.GetFileName(rename.Key)} -> {Path.GetFileName(rename.Value)}");
            }
            FileNameNormalizer.PerformRenames(fileRenames);
        }
        else
        {
            Console.WriteLine("No files need to be renamed.");
        }

        Console.WriteLine($"Completed in {DateTime.Now - start}");
    }

    private static ReuseableRewriterPool Pool(IReuseableRewriter rewriter) => new(rewriter);
}