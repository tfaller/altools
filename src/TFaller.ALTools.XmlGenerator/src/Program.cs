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
        var generateCommand = new Command("generate", "Generate the XML output")
        {
            configArgument
        };
        generateCommand.SetAction(async parseResult =>
        {
            var config = Config.LoadConfig(parseResult.GetValue(configArgument)!);
            var generator = new ActionGenerate(config);
            await generator.Generate();
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