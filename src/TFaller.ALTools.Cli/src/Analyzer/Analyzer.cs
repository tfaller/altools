namespace TFaller.ALTools.Cli.Analyzer;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.CommandLine;
using System.Threading.Tasks;
using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.Diagnostics;
using Microsoft.Dynamics.Nav.CodeAnalysis.Syntax;
using TFaller.ALTools.Transformation;

internal static class Analyzer
{
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true
    };

    public async static Task Analyze(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("analyzer requires workspace path as first argument");

        // Use System.CommandLine to parse arguments: workspace (positional), --gitlabReport, --suppress
        var workspaceArg = new Argument<string>("workspace") { Arity = ArgumentArity.ExactlyOne };
        var gitlabOption = new Option<string?>("--gitlabReport") { Description = "Path to write GitLab code-quality JSON" };
        var suppressOption = new Option<string[]>("--suppress") { Description = "Suppress diagnostic IDs (can be passed multiple times or comma-separated)", Arity = ArgumentArity.ZeroOrMore };

        var root = new RootCommand
        {
            workspaceArg,
            gitlabOption,
            suppressOption
        };

        var parseResult = root.Parse(args);
        var workspace = parseResult.GetValue(workspaceArg) ?? throw new ArgumentException("workspace path is required");
        var gitlabReportPath = parseResult.GetValue(gitlabOption);
        var suppressValues = parseResult.GetValue(suppressOption) ?? [];

        var suppressIdsList = new List<string>();
        foreach (var supress in suppressValues)
        {
            suppressIdsList.AddRange(supress.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        var supresssIds = suppressIdsList.ToArray();

        var comp = Compilation.Create("tmp");
        var files = new Dictionary<string, SyntaxTree>();

        comp = WorkspaceHelper.LoadReferences(comp, workspace + "/.alpackages");
        comp = await WorkspaceHelper.LoadFilesAsync(comp, workspace, null!, files);

        comp = comp.WithOptions(comp.Options.WithSpecificDiagnosticOptions(
            supresssIds.ToImmutableDictionary(item => item, _ => ReportDiagnostic.Suppress)
        ));

        var analyzerFile = new AnalyzerFileReference(
            AssemblyLoader.AnalyzerFullPathByName("Microsoft.Dynamics.Nav.CodeCop"),
            new AnalyzerAssemblyLoader());

        analyzerFile.AnalyzerLoadFailed += (sender, e) =>
        {
            Console.WriteLine($"Failed to load analyzer: {e.Message}");
        };

        var compAnalyzerOptions = new CompilationWithAnalyzersOptions(
            new AnalyzerOptions([]),
            onAnalyzerException: null!,
            concurrentAnalysis: true,
            logAnalyzerExecutionTime: false,
            reportSuppressedDiagnostics: false
        );

        var compWithAnalyzers = new CompilationWithAnalyzers(comp, analyzerFile.GetAnalyzers(), compAnalyzerOptions);

        var diagnostics = await compWithAnalyzers.GetAllDiagnosticsAsync();

        var issues = new List<GitlabCodeQualityIssue>();
        var basePath = Environment.GetEnvironmentVariable("CI_PROJECT_DIR") ?? ".";

        foreach (var diag in diagnostics)
        {
            if (diag.Severity == DiagnosticSeverity.Hidden)
                continue;

            if (diag.IsSuppressed)
                continue;

            Console.WriteLine(diag.ToString());

            if (gitlabReportPath != null)
            {
                var issue = ConvertDiagnosticToIssue(diag, basePath);
                if (issue != null)
                    issues.Add(issue);
            }
        }

        if (gitlabReportPath != null)
        {
            var json = JsonSerializer.Serialize(issues, jsonOptions);
            await File.WriteAllTextAsync(gitlabReportPath, json, Encoding.UTF8);
        }
    }

    private static GitlabCodeQualityIssue? ConvertDiagnosticToIssue(Diagnostic diag, string basePath)
    {
        var location = diag.Location;
        var locaionLineSpan = location?.GetLineSpan();
        var path = location?.SourceTree?.FilePath ?? locaionLineSpan?.Path ?? string.Empty;
        if (string.IsNullOrEmpty(path))
            return null; // skip diagnostics without source file

        path = Path.GetRelativePath(basePath, path);

        var line = locaionLineSpan?.StartLinePosition.Line ?? 0;
        var checkName = diag.Id ?? diag.Descriptor?.Id ?? "unknown";
        var description = diag.GetMessage() ?? string.Empty;

        // fingerprint: md5 of checkName + description + path + line
        var fingerprintSource = $"{checkName}\u0000{description}\u0000{path}\u0000{line}";
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(fingerprintSource));
        var fingerprint = Convert.ToHexStringLower(hash);

        var severity = diag.Severity switch
        {
            DiagnosticSeverity.Error => GitlabSeverity.Major,
            DiagnosticSeverity.Warning => GitlabSeverity.Minor,
            DiagnosticSeverity.Info => GitlabSeverity.Info,
            _ => GitlabSeverity.Info,
        };

        var issue = new GitlabCodeQualityIssue
        {
            Description = description,
            CheckName = checkName,
            Fingerprint = fingerprint,
            Severity = severity,
            Location = new()
            {
                Path = path,
                Lines = new() { Begin = line }
            }
        };

        return issue;
    }

    internal sealed class AnalyzerAssemblyLoader : IAnalyzerAssemblyLoader
    {
        public void AddDependencyLocation(string fullPath)
        {
            // The analyzers we use don't care about dependencies
        }

        public Assembly LoadFromPath(string fullPath)
        {
            // Load by name, so the regular AL extension assembly loader is used
            return Assembly.Load(Path.GetFileNameWithoutExtension(fullPath));
        }
    }
}