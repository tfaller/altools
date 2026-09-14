using System;
using System.Threading.Tasks;
using System.Diagnostics;
using TFaller.ALTools.Transformation;

namespace TFaller.ALTools.OpenApiGenerator;

public class Program
{
    enum ExitCodes : int
    {
        Sucesss = 0,
        NoConfig = 1,
        InvalidOperation = 2,
    }

    public static async Task Main(string[] args)
    {
        AssemblyLoader.RegisterLoader();

        if (args.Length < 2)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("no operation given: generate");
            }
            Console.WriteLine("no config file given");
            Environment.Exit((int)ExitCodes.NoConfig);
        }

        var config = Config.LoadConfig(args[1]);

        // Compare the configured output-format version to a fixed expected output version.
        // This is a manual bump target that indicates a change in the generator's output format.
        const string ExpectedOutputVersion = "1.0.0";

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

        switch (args[0])
        {
            case "generate":
                var generator = new ActionGenerate(config);
                await generator.Generate();
                break;

            default:
                Console.WriteLine("invalid operation given: generate");
                Environment.Exit((int)ExitCodes.InvalidOperation);
                break;
        }

        Environment.Exit((int)ExitCodes.Sucesss);
    }
}