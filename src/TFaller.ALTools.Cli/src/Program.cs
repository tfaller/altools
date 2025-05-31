namespace TFaller.ALTools.Cli;

using System;
using System.Threading.Tasks;

public class Program
{
    public static Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("No command passed");
            Environment.Exit(1);
        }

        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            Console.Error.WriteLine($"Unhandled Exception: {e.ExceptionObject}");
            Environment.Exit(1);
        };

        return args[0] switch
        {
            "openapi" => OpenApiGenerator.Program.Main(args[1..]),
            "xml" => XmlGenerator.Program.Main(args[1..]),
            _ => throw new ArgumentException($"Unknown command: {args[0]}, please use 'openapi|xml'"),
        };
    }
}