namespace TFaller.ALTools.Cli;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TFaller.ALTools.Transformation;
using TFaller.ALTools.Transformation.Rewriter;
using TFaller.ALTools.Transformation.Transformer.CommentRule;

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
            Console.Error.WriteLine("       workspace-transform <workspace-path> config <transformation-name>");
            Environment.Exit(1);
        }

        var workspacePath = args[0];
        var transformName = args[1];

        if (string.Equals(transformName, "file-name-normalizer", StringComparison.OrdinalIgnoreCase))
        {
            await RunFileNameNormalizer(workspacePath);
            return;
        }

        if (string.Equals(transformName, "config", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine("When using 'config' as rewriter, you also need to specify the transformation name defined in the config file");
                Environment.Exit(1);
            }

            await RunConfigBasedTransformation(workspacePath, args[2]);
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

    private static async Task RunConfigBasedTransformation(string workspacePath, string transformationName)
    {
        var config = WorkspaceConfig.LoadConfig(Path.Combine(workspacePath, "altools.json"));
        if (!config.Transformations.TryGetValue(transformationName, out var rewriterConfigs) || rewriterConfigs is null)
        {
            Console.Error.WriteLine($"No transformation named '{transformationName}' found in config.");
            Environment.Exit(1);
        }

        var rewriters = new List<IConcurrentRewriter>();
        foreach (var rewriterConfig in rewriterConfigs)
        {
            if (rewriterConfig is RewriterCommentTransformVar commentTransformVar)
            {
                var tags = commentTransformVar.Tags?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>();
                rewriters.Add(Pool(new TransformVar(new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase))));
                continue;
            }

            if (rewriterConfig is RewriterComplexReturnTranspiler)
            {
                rewriters.Add(Rewriters["complex-return-transpiler"].Value);
                continue;
            }

            if (rewriterConfig is RewriterComplexReturnUplifter)
            {
                rewriters.Add(Rewriters["complex-return-uplifter"].Value);
                continue;
            }

            Console.Error.WriteLine($"Unknown rewriter type in config: {rewriterConfig.GetType().Name}");
            Environment.Exit(1);
        }

        var start = DateTime.Now;

        Console.WriteLine($"Starting transformation '{transformationName}'...");

        var rewriter = new WorkspaceRewriter(rewriters, null!);
        await rewriter.Rewrite(workspacePath);

        Console.WriteLine($"Completed transformation '{transformationName}' in {DateTime.Now - start}");
    }

    private static ReuseableRewriterPool Pool(IReuseableRewriter rewriter) => new(rewriter);
}