using System;
using System.Threading.Tasks;
using TFaller.ALTools.Transformation;

namespace TFaller.ALTools.Cli;

internal static class InferReferencesCommand
{
    public static async Task Execute(string[] args)
    {
        if (args.Length < 5)
        {
            Console.Error.WriteLine("Usage: infer-references <workspace-path> <output-path> <module-name> <publisher> <version> [options]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Arguments:");
            Console.Error.WriteLine("  workspace-path : Path to the AL workspace directory");
            Console.Error.WriteLine("  output-path    : Path where output should be saved");
            Console.Error.WriteLine("  module-name    : Name of the module to generate");
            Console.Error.WriteLine("  publisher      : Publisher of the module");
            Console.Error.WriteLine("  version        : Version of the module (e.g., 1.0.0.0)");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Options:");
            Console.Error.WriteLine("  --json           : Export to JSON file instead of .app package");
            Console.Error.WriteLine("  --module-id <id> : Specify GUID for the module");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Examples:");
            Console.Error.WriteLine("  # Generate .app reference package:");
            Console.Error.WriteLine("  infer-references ./MyApp ./output/MyAppRefs.app \"MyApp References\" \"MyCompany\" 1.0.0.0");
            Console.Error.WriteLine();
            Console.Error.WriteLine("  # Export to JSON for inspection:");
            Console.Error.WriteLine("  infer-references ./MyApp ./output/refs.json \"MyApp References\" \"MyCompany\" 1.0.0.0 --json");
            Environment.Exit(1);
        }

        var workspacePath = args[0];
        var outputPath = args[1];
        var moduleName = args[2];
        var publisher = args[3];
        var version = args[4];

        Guid? moduleId = null;
        bool exportJson = false;

        // Parse options
        for (int i = 5; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--json":
                    exportJson = true;
                    break;
                case "--module-id":
                    if (i + 1 < args.Length && Guid.TryParse(args[i + 1], out var parsedGuid))
                    {
                        moduleId = parsedGuid;
                        i++; // Skip next argument
                    }
                    else
                    {
                        Console.Error.WriteLine($"Invalid module ID");
                        Environment.Exit(1);
                    }
                    break;
                default:
                    if (Guid.TryParse(args[i], out var guid))
                    {
                        moduleId = guid;
                    }
                    else
                    {
                        Console.Error.WriteLine($"Unknown option: {args[i]}");
                        Environment.Exit(1);
                    }
                    break;
            }
        }

        try
        {
            var start = DateTime.Now;

            if (exportJson)
            {
                // Generate and export to JSON
                var moduleDefinition = await InferReferences.InferFromWorkspace(
                    workspacePath,
                    moduleName,
                    publisher,
                    version,
                    moduleId);

                InferReferences.ExportToJson(moduleDefinition, outputPath);
            }
            else
            {
                // Generate .app package
                await InferReferences.GenerateReferencePackage(
                    workspacePath,
                    outputPath,
                    moduleName,
                    publisher,
                    version,
                    moduleId);
            }

            Console.WriteLine($"\nCompleted in {DateTime.Now - start}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }
}