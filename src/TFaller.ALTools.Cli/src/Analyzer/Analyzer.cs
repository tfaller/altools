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
using System.Linq;
using Microsoft.Dynamics.Nav.CodeAnalysis.DiagnosticRules;
using Microsoft.Dynamics.Nav.CodeAnalysis.DotNet;

internal static class Analyzer
{
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly Dictionary<string, string> copAssemblies = new()
    {
        ["AppSourceCop"] = "Microsoft.Dynamics.Nav.AppSourceCop",
        ["CodeCop"] = "Microsoft.Dynamics.Nav.CodeCop",
        ["PerTenantExtensionCop"] = "Microsoft.Dynamics.Nav.PerTenantExtensionCop",
        ["UICop"] = "Microsoft.Dynamics.Nav.UICop"
    };

    public async static Task Analyze(string[] args)
    {
        var workspaceArg = new Argument<string>("workspace") { Arity = ArgumentArity.ExactlyOne };
        var gitlabOption = new Option<string?>("--gitlabReport") { Description = "Path to write GitLab code-quality JSON" };
        var suppressOption = new Option<string[]>("--suppress") { Description = "Suppress diagnostic IDs (can be passed multiple times or comma-separated)", Arity = ArgumentArity.ZeroOrMore };
        var ruleSet = new Option<string>("--ruleSet") { Description = "Path to the rule set file", Arity = ArgumentArity.ZeroOrOne };
        var ruleSetAllowExternal = new Option<bool>("--ruleSetAllowExternal") { Description = "Allow external rules in the rule set", Arity = ArgumentArity.ZeroOrOne };
        var analyzers = new Option<string[]>("--analyzer", "-a") { Description = "Specify which analyzers to run", Arity = ArgumentArity.ZeroOrMore, DefaultValueFactory = (_) => [.. copAssemblies.Keys] };
        analyzers.AcceptOnlyFromAmong([.. copAssemblies.Keys]);

        var root = new RootCommand
        {
            workspaceArg,
            gitlabOption,
            suppressOption,
            analyzers,
            ruleSet,
        };
        root.TreatUnmatchedTokensAsErrors = true;
        root.SetAction(async parseResult =>
        {
            return await AnalyzeAction(
                parseResult.GetValue(workspaceArg) ?? throw new ArgumentException("workspace path is required"),
                parseResult.GetValue(suppressOption) ?? [],
                parseResult.GetValue(gitlabOption),
                parseResult.GetValue(analyzers) ?? [.. copAssemblies.Keys],
                parseResult.GetValue(ruleSet),
                parseResult.GetValue(ruleSetAllowExternal)
            );
        });

        var parseResult = root.Parse(args);
        Environment.Exit(await parseResult.InvokeAsync());
    }

    private async static Task<int> AnalyzeAction(string workspace, string[] suppressValues, string? gitlabReportPath, string[] analyzerNames, string? ruleSetPath, bool ruleSetAllowExternal)
    {
        var suppressIdsList = new List<string>();
        foreach (var supress in suppressValues)
        {
            suppressIdsList.AddRange(supress.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        var supresssIds = suppressIdsList.ToArray();

        var manifest = await WorkspaceHelper.LoadAppManifestAsync(workspace);
        var comp = Compilation.Create(manifest.AppName);
        var files = new Dictionary<string, SyntaxTree>();

        comp = comp.WithOptions(comp.Options.WithManifestOptions(manifest));
        comp = WorkspaceHelper.LoadReferences(comp, Path.Combine(workspace, ".alpackages"));
        comp = comp.WithDotNetResolverFactory(new DotNetResolverFactory(new AssemblyLocator([Path.Combine(workspace, ".netpackages")])));
        comp = await WorkspaceHelper.LoadFilesAsync(comp, workspace, new ParseOptions(manifest.Runtime), files);

        comp = comp.WithOptions(comp.Options.WithSpecificDiagnosticOptions(
            supresssIds.ToImmutableDictionary(item => item, _ => ReportDiagnostic.Suppress)
        ));

        if (!string.IsNullOrWhiteSpace(ruleSetPath))
        {
            var ruleSetDiagnostic = new List<Diagnostic>();
            var ruleSet = RuleSetResolver.Load(ruleSetPath, ruleSetDiagnostic, ruleSetAllowExternal);

            if (ruleSetDiagnostic.Count > 0)
            {
                Console.Error.WriteLine("Errors occurred while loading the rule set:");
                foreach (var diag in ruleSetDiagnostic)
                {
                    Console.Error.WriteLine(diag.ToString());
                }
                return 1;
            }

            comp = comp.WithOptions(comp.Options.WithSpecificDiagnosticOptions(
                comp.Options.SpecificDiagnosticOptions.Union(ruleSet.SpecificDiagnosticOptions).ToImmutableDictionary()
            ));
        }

        var compAnalyzerOptions = new CompilationWithAnalyzersOptions(
            new AnalyzerOptions([]),
            onAnalyzerException: null!,
            concurrentAnalysis: true,
            logAnalyzerExecutionTime: false,
            reportSuppressedDiagnostics: false
        );

        var copAnalyzers = analyzerNames.Select(name => AnalyzerAssemblyLoader.GetAnalyzersByAssemblyName(copAssemblies[name]))
            .SelectMany(cop => cop)
            .ToImmutableArray();

        var compWithAnalyzers = new CompilationWithAnalyzers(comp, copAnalyzers, compAnalyzerOptions);
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

        return issues.Count == 0 ? 0 : 1;
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

        public static ImmutableArray<DiagnosticAnalyzer> GetAnalyzersByAssemblyName(string assemblyName)
        {
            var analyzerFile = new AnalyzerFileReference(
                AssemblyLoader.AnalyzerFullPathByName(assemblyName),
                new AnalyzerAssemblyLoader());

            analyzerFile.AnalyzerLoadFailed += (sender, e) =>
            {
                throw new Exception($"Failed to load analyzer: {e.Message}", e.Exception);
            };

            return analyzerFile.GetAnalyzers();
        }
    }
}