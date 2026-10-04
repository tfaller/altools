using System;
using System.CommandLine;
using System.Threading.Tasks;
using TFaller.ALTools.Transformation;

namespace TFaller.ALTools.XmlGenerator;

public class Program
{
    public static Task<int> Main(string[] args)
    {
        AssemblyLoader.RegisterLoader();

        var configArgument = new Argument<string>("config")
        {
            Description = "Path to the XML generator configuration file",
        };
        var checkOption = new Option<bool?>("--check")
        {
            Description = "Verify that the generated files are up to date without writing them. Fails with a non-zero exit code if not, useful for CI pipelines.",
        };
        var generateCommand = new Command("generate", "Generate the XML output")
        {
            configArgument,
            checkOption
        };
        generateCommand.SetAction(async parseResult =>
        {
            var config = Config.LoadConfig(parseResult.GetValue(configArgument)!);
            var check = parseResult.GetValue(checkOption) ?? false;
            var generator = new ActionGenerate(config);
            var upToDate = await generator.Generate(check);

            if (check && !upToDate)
            {
                Console.Error.WriteLine("Generated files are not up to date. Run 'generate' without --check to update them.");
                return 1;
            }

            return 0;
        });

        var rootCommand = new RootCommand("Generate XML output from an AL workspace")
        {
            generateCommand
        };
        rootCommand.TreatUnmatchedTokensAsErrors = true;

        return rootCommand.Parse(args).InvokeAsync();
    }
}