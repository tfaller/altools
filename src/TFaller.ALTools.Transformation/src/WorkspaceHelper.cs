using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.Packaging;
using Microsoft.Dynamics.Nav.CodeAnalysis.SymbolReference;
using Microsoft.Dynamics.Nav.CodeAnalysis.Syntax;
using System.Collections.Generic;
using System.IO;
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

    public static async Task<Compilation> LoadFilesAsync(Compilation comp, string path, ParseOptions parseOptions, Dictionary<string, SyntaxTree> files)
    {
        foreach (var file in Directory.GetFiles(path, "*.al", SearchOption.AllDirectories))
        {
            var content = await File.ReadAllTextAsync(file, Encoding.UTF8);
            var syntaxTree = SyntaxTree.ParseObjectText(content, file, Encoding.UTF8, parseOptions);
            files.Add(file, syntaxTree);
            comp = comp.AddSyntaxTrees(syntaxTree);
        }
        return comp;
    }
}