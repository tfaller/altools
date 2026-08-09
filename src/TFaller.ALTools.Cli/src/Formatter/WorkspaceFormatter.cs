using System;
using System.CommandLine;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.Syntax;
using TFaller.ALTools.Transformation;
using TFaller.ALTools.Transformation.Rewriter;

namespace TFaller.ALTools.Cli.Formatter;

public static class WorkspaceFormatter
{
    public static async Task Format(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Usage: format <workspace-path>");
            Environment.Exit(1);
        }

        var workspaceArg = new Argument<string>("workspace") { Arity = ArgumentArity.ExactlyOne };
        var checkOption = new Option<bool?>("--check") { Description = "Just check mode, not actually format" };

        var root = new RootCommand
        {
            workspaceArg,
            checkOption
        };

        var parseResult = root.Parse(args);
        var workspace = parseResult.GetValue(workspaceArg) ?? throw new ArgumentException("workspace path is required");
        var checkMode = parseResult.GetValue(checkOption) ?? false;

        var formatter = new Transformation.Formatter();

        var statsFormattedFiles = 0;
        var statsUnformattedFiles = 0;

        await foreach (var kvp in WorkspaceHelper.LoadFilesAsStringAsync(workspace))
        {
            var file = kvp.Item1;
            var syntaxNode = SyntaxTree.ParseObjectText(kvp.Item2, file, Encoding.UTF8, ParseOptions.Default, default).GetRoot();
            var formattedNode = formatter.Format(syntaxNode);

            if (!formattedNode.GetText().ContentEquals(syntaxNode.GetText()))
            {
                if (checkMode)
                {
                    Console.WriteLine($"{file}: not formatted");
                    statsUnformattedFiles++;
                }
                else
                {
                    await File.WriteAllTextAsync(file, formattedNode.ToFullString(), WorkspaceRewriter.Encoding);
                    Console.WriteLine($"{file}: formatted");
                    statsUnformattedFiles++;
                }
            }
            else
            {
                statsFormattedFiles++;
            }
        }

        if (checkMode)
        {
            Console.WriteLine($"Checked {statsFormattedFiles + statsUnformattedFiles} files, {statsUnformattedFiles} not formatted, {statsFormattedFiles} already formatted");
            Environment.Exit(statsUnformattedFiles > 0 ? 1 : 0);
        }

        Console.WriteLine($"Checked {statsFormattedFiles + statsUnformattedFiles} files, {statsUnformattedFiles} formatted, {statsFormattedFiles} already formatted");
    }
}