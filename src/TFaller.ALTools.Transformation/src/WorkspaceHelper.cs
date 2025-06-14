using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.Packaging;
using Microsoft.Dynamics.Nav.CodeAnalysis.SymbolReference;
using Microsoft.Dynamics.Nav.CodeAnalysis.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TFaller.ALTools.Transformation;

public class WorkspaceHelper
{
    public static Compilation LoadReferences(Compilation comp, string alpackagesPath)
    {
        comp = comp.WithReferenceManager(ReferenceManagerFactory.Create([], comp));
        comp = comp.WithReferenceLoader(ReferenceLoaderFactory.CreateReferenceLoader([alpackagesPath]));

        foreach (var package in Directory.GetFiles(alpackagesPath, "*.app"))
        {
            using var appStream = File.Open(package, FileMode.Open);
            using var reader = NavAppPackageReader.Create(appStream);

            var info = reader.ReadNavAppManifest();
            comp = comp.AddReferences(info.GetAllReferences());

            // add package itself ... is probably not useless in folder
            comp = comp.AddReferences(new SymbolReferenceSpecification(
                info.AppPublisher, info.AppName, info.AppVersion, true, info.AppId
            ));
        }

        return comp;
    }

    public static Compilation LoadFiles(Compilation comp, string path, ParseOptions parseOptions, Dictionary<string, SyntaxTree> files)
    {
        return LoadFilesAsync(comp, path, parseOptions, files).Result;
    }

    /// <summary>
    /// Loads AL files as string. Yields for each loaded file.
    /// The loading order is random.
    /// </summary>
    /// <param name="path">Path where files should be searched</param>
    /// <returns>Loaded files as string</returns>
    public static async IAsyncEnumerable<ValueTuple<string, string>> LoadFilesAsStringAsync(string path)
    {
        var sources = Directory.GetFiles(path, "*.al", SearchOption.AllDirectories).Select(async file =>
                new ValueTuple<string, string>(file, await File.ReadAllTextAsync(file, Encoding.UTF8))
        ).ToLinkedList();

        while (sources.Count > 0)
        {
            var completedTask = await Task.WhenAny(sources);
            sources.Remove(completedTask);
            yield return await completedTask;
        }
    }

    public static async Task<Compilation> LoadFilesAsync(Compilation comp, string path, ParseOptions parseOptions, Dictionary<string, SyntaxTree> files)
    {
        if (files.Count > 0)
        {
            throw new ArgumentException("files dictionary must be empty", nameof(files));
        }

        await Parallel.ForEachAsync(LoadFilesAsStringAsync(path), (kvp, token) =>
        {
            var file = kvp.Item1;
            var syntaxTree = SyntaxTree.ParseObjectText(kvp.Item2, file, Encoding.UTF8, parseOptions, token);

            lock (files)
            {
                files.Add(file, syntaxTree);
            }

            return new ValueTask();
        });

        return comp.AddSyntaxTrees(files.Values);
    }
}