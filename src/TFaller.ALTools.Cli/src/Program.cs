namespace TFaller.ALTools.Cli;

using System;
using System.Threading.Tasks;
using TFaller.ALTools.Transformation;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("No command passed");
            return 1;
        }

        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            Console.Error.WriteLine($"Unhandled Exception: {e.ExceptionObject}");
            Environment.Exit(1);
        };

        AssemblyLoader.RegisterLoader();

        return await (args[0] switch
        {
            "analyzer" => Analyzer.Analyzer.Analyze(args[1..]),
            "download-symbols" => DownloadSymbols.Download(args[1..]),
            "formatter" => Formatter.WorkspaceFormatter.Format(args[1..]),
            "infer" => InferReferencesCommand.Execute(args[1..]),
            "openapi" => OpenApiGenerator.Program.Main(args[1..]),
            "xml" => XmlGenerator.Program.Main(args[1..]),
            "workspace-transformation" => WorkspaceTransformation.Transform(args[1..]),
            _ => throw new ArgumentException($"Unknown command: {args[0]}, please use 'analyzer|openapi|xml|workspace-transformation|infer'"),
        });
    }
}