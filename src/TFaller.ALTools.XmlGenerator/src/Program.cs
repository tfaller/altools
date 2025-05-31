using System;
using System.Threading.Tasks;
using TFaller.ALTools.Transformation;

namespace TFaller.ALTools.XmlGenerator;

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