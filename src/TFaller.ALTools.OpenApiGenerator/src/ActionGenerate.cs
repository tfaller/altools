using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.CommandLine;
using Microsoft.Dynamics.Nav.CodeAnalysis.Diagnostics;
using Microsoft.Dynamics.Nav.CodeAnalysis.Syntax;
using Microsoft.OpenApi.Interfaces;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Reader;
using Microsoft.OpenApi.Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TFaller.ALTools.Transformation;

namespace TFaller.ALTools.OpenApiGenerator;

public class ActionGenerate
{
    private static readonly OpenApiReaderSettings _readerSettings = new()
    {
        Readers = new Dictionary<string, IOpenApiReader>{
            { OpenApiConstants.Yaml, new OpenApiYamlReader() },
        },
    };

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

    public async Task Generate()
    {
        var start = DateTime.Now;

        if (_config.Definitions == null)
        {
            throw new InvalidOperationException("no definitions found");
        }

        await Parallel.ForEachAsync(_config.Definitions, async (definition, token) =>
        {
            await GenerateCodeunit(definition);
        });

        Console.WriteLine(string.Format("Finished all code generation in {0}!", DateTime.Now - start));
    }

    private async Task GenerateCodeunit(Definition definition)
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

        var document = await LoadOpenApiDocument(RelativePath(schemaFile));
        TransformInlineTypes.Transform(document);

        // generate the all codeunits

        var symbolGen = new Generator
        {
            GenerateValidate = definition.GenerateValidate
        };
        symbolGen.AddComponents(document.Components ?? throw new InvalidOperationException("no components found"));

        // merge all codeunits to a single one

        var compUnit = SyntaxFactory.ParseCompilationUnit(symbolGen.GetCode(), 0, _parseOptions)
            ?? throw new InvalidOperationException("no codeunit found");

        var generatedCodeunits = compUnit.Objects.OfType<CodeunitSyntax>().ToArray();

        var merged = CodeunitMergeRewriter.Merge(definition.MergedCodeunitName, definition.MergedCodeunitId.Value, generatedCodeunits);

        // final generated result
        await File.WriteAllTextAsync(RelativePath(definition.MergedCodeunitFile), _formatter.Format(merged).ToFullString());
        Log(schemaFile, "Finished generation");
    }

    private async Task<OpenApiDocument> LoadOpenApiDocument(string schemaFile)
    {
        var result = await OpenApiDocument.LoadAsync(schemaFile, _readerSettings);
        var diagnostic = result.Diagnostic;

        if (diagnostic?.Warnings is not null)
        {
            foreach (var warning in diagnostic.Warnings)
            {
                Log(schemaFile, string.Format("[WARN] {0}: {1}", warning.Pointer, warning.Message));
            }
        }

        if (diagnostic?.Errors is not null)
        {
            var hasErros = false;

            foreach (var err in diagnostic.Errors)
            {
                hasErros = true;
                Log(schemaFile, string.Format("[ERRO] {0}: {1}", err.Pointer, err.Message));
            }

            if (hasErros)
            {
                throw new InvalidOperationException("schema errors found");
            }
        }

        return result.Document ?? throw new InvalidOperationException("docment is null");
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