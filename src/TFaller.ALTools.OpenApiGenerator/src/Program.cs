using System;
using System.CommandLine;
using System.Threading.Tasks;
using System.Diagnostics;
using TFaller.ALTools.Transformation;

namespace TFaller.ALTools.OpenApiGenerator;

public class Program
{
    // Compare the configured output-format version to a fixed expected output version.
    // This is a manual bump target that indicates a change in the generator's output format.
    const string ExpectedOutputVersion = "1.0.2";

    public static Task<int> Main(string[] args)
    {
        AssemblyLoader.RegisterLoader();

        var configArgument = new Argument<string>("config")
        {
            Description = "Path to the OpenAPI generator configuration file",
        };
        var checkOption = new Option<bool?>("--check")
        {
            Description = "Verify that the generated files are up to date without writing them. Fails with a non-zero exit code if not, useful for CI pipelines.",
        };
        var generateCommand = new Command("generate", "Generate the OpenAPI output")
        {
            configArgument,
            checkOption
        };
        generateCommand.SetAction(async parseResult =>
        {
            var config = Config.LoadConfig(parseResult.GetValue(configArgument)!);
            var check = parseResult.GetValue(checkOption) ?? false;
            return await Generate(config, check);
        });

        var rootCommand = new RootCommand("Generate OpenAPI output from an AL workspace")
        {
            generateCommand
        };
        rootCommand.TreatUnmatchedTokensAsErrors = true;

        return rootCommand.Parse(args).InvokeAsync();
    }

    private static async Task<int> Generate(Config config, bool check)
    {
        // Default missing output version to 0.0.0 to simplify checks downstream.
        var cfgVerStr = string.IsNullOrWhiteSpace(config.OutputVersion) ? "0.0.0" : config.OutputVersion!.Trim();

        if (string.Equals(cfgVerStr, "0.0.0", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"WARNING: config does not contain 'outputVersion'. Expected output version: '{ExpectedOutputVersion}'.");
        }
        else if (!string.Equals(cfgVerStr, ExpectedOutputVersion, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"WARNING: outputVersion '{cfgVerStr}' does not match expected output version '{ExpectedOutputVersion}'.");
        }

        if ((string.Equals(cfgVerStr, "0.0.0", StringComparison.OrdinalIgnoreCase) || !string.Equals(cfgVerStr, ExpectedOutputVersion, StringComparison.OrdinalIgnoreCase)) && Debugger.IsAttached)
        {
            Console.WriteLine("Debugger attached — breaking into debugger to notify about output-version issue.");
            Debugger.Break();
        }

        var generator = new ActionGenerate(config);
        var upToDate = await generator.Generate(check);

        if (check && !upToDate)
        {
            Console.Error.WriteLine("Generated files are not up to date. Run 'generate' without --check to update them.");
            return 1;
        }

        return 0;
    }
}