namespace TFaller.ALTools.Cli;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.Diagnostics;
using Microsoft.Dynamics.Nav.CodeAnalysis.Syntax;
using TFaller.ALTools.Transformation;

internal static class Analyzer
{
    public async static Task Analyze(string[] args)
    {
        var workspace = args[0];
        var supresssIds = args.Length > 1 ? args[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : [];

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
        foreach (var diag in diagnostics)
        {
            if (diag.Severity == DiagnosticSeverity.Hidden)
                continue;

            if (diag.IsSuppressed)
                continue;

            Console.WriteLine(diag.ToString());
        }
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