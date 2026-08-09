using System;
using System.CommandLine;
using System.Threading.Tasks;

namespace TFaller.ALTools.Cli;

public static class DownloadSymbols
{
    public static async Task Download(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Usage: download-symbols <workspace-path> --url <connection-url>");
            Environment.Exit(1);
        }

        var workspaceArg = new Argument<string>("workspace") { Arity = ArgumentArity.ExactlyOne };
        var urlOption = new Option<string>("--url") { Description = "Connection URI" };

        var root = new RootCommand
        {
            workspaceArg,
            urlOption
        };

        var parseResult = root.Parse(args);
        var workspace = parseResult.GetValue(workspaceArg) ?? throw new ArgumentException("workspace path is required");
        var url = parseResult.GetValue(urlOption) ?? throw new ArgumentException("connection URI is required");

        await Transformation.Deployment.DownloadSymbols.DownloadSymbolsAsync(workspace, url);
    }
}