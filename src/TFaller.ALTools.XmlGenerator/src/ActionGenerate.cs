using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.CommandLine;
using Microsoft.Dynamics.Nav.CodeAnalysis.Diagnostics;
using Microsoft.Dynamics.Nav.CodeAnalysis.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using TFaller.ALTools.Transformation;

namespace TFaller.ALTools.XmlGenerator;

public class ActionGenerate
{
    private readonly Config _config;
    private readonly ProjectManifest _projectManifest;
    private readonly ParseOptions _parseOptions;
    private static readonly Formatter _formatter = new();

    public ActionGenerate(Config config)
    {
        _config = config;

        _projectManifest = LoadProjectManifest(config.ProjectPath);
        _parseOptions = new ParseOptions(_projectManifest.AppManifest.Runtime);
    }

    public async Task<bool> Generate(bool check = false)
    {
        var start = DateTime.Now;

        if (_config.Definitions == null)
        {
            throw new InvalidOperationException("no definitions found");
        }

        var upToDateCount = 0;
        var outdatedCount = 0;

        await Parallel.ForEachAsync(_config.Definitions, async (definition, token) =>
        {
            var isUpToDate = await GenerateCodeunit(definition, check);
            if (isUpToDate)
            {
                Interlocked.Increment(ref upToDateCount);
            }
            else
            {
                Interlocked.Increment(ref outdatedCount);
            }
        });

        if (check)
        {
            Console.WriteLine(string.Format("Checked {0} definitions in {1}: {2} up to date, {3} outdated", upToDateCount + outdatedCount, DateTime.Now - start, upToDateCount, outdatedCount));
        }
        else
        {
            Console.WriteLine(string.Format("Finished all code generation in {0}!", DateTime.Now - start));
        }

        return outdatedCount == 0;
    }

    private async Task<bool> GenerateCodeunit(Definition definition, bool check)
    {
        if (definition.SchemaFile == null)
        {
            throw new InvalidOperationException("no schema file given");
        }
        if (definition.MergedCodeunitName == null)
        {
            throw new InvalidOperationException("no merged codeunit name given");
        }
        if (definition.MergedCodeunitId == null)
        {
            throw new InvalidOperationException("no merged codeunit id given");
        }
        if (definition.MergedCodeunitFile == null)
        {
            throw new InvalidOperationException("no merged codeunit file given");
        }

        // prepare the schema file
        var schemaFile = RelativePath(definition.SchemaFile);
        Log(schemaFile, "Starting code generation");

        var document = new XmlDocument();
        document.Load(RelativePath(schemaFile));

        // generate the all codeunits

        var symbolGen = new Generator(document, definition.SchemaSettings.Select(s => (s.Key, s.Value.TypeRenamePatterns)).ToDictionary());
        symbolGen.Generate();

        // merge all codeunits to a single one

        var compUnit = SyntaxFactory.ParseCompilationUnit(symbolGen.GetCode(), 0, _parseOptions)
            ?? throw new InvalidOperationException("no codeunit found");

        var generatedCodeunits = compUnit.Objects.OfType<CodeunitSyntax>().ToArray();

        var merged = CodeunitMergeRewriter.Merge(definition.MergedCodeunitName, definition.MergedCodeunitId.Value, generatedCodeunits);

        // final generated result
        var path = RelativePath(definition.MergedCodeunitFile);
        var isUpToDate = await GeneratedFileWriter.WriteOrCheck(path, _formatter.Format(merged).ToFullString(), check);
        if (!isUpToDate)
        {
            Log(schemaFile, string.Format("File is out of date: {0}", path));
        }
        Log(schemaFile, "Finished generation");
        return isUpToDate;
    }

    private string RelativePath(string file)
    {
        if (Path.IsPathRooted(file) == false)
        {
            file = Path.Combine(_config.ProjectPath, file);
        }
        return file;
    }

    private void Log(string schemaFile, string message)
    {
        schemaFile = Path.GetRelativePath(_config.ProjectPath, schemaFile);
        Console.WriteLine(string.Format("{0}: {1}", schemaFile, message));
    }

    private static ProjectManifest LoadProjectManifest(string projectPath)
    {
        var diagnostics = new List<Diagnostic>();
        var projectManifest = ProjectLoader.LoadFromFolder(projectPath, diagnostics);

        if (projectManifest == null)
        {
            Console.Error.WriteLine("Errors while loading project from path '" + projectPath + "':");

            foreach (var diagnostic in diagnostics)
            {
                Console.Error.WriteLine(diagnostic.ToString());
            }

            throw new InvalidOperationException("Errors while loading project manifest");
        }

        return projectManifest;
    }
}