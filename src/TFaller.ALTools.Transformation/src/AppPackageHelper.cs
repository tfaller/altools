using Microsoft.Dynamics.Nav.CodeAnalysis.Packaging;
using Microsoft.Dynamics.Nav.CodeAnalysis.SymbolReference;
using System.IO;

namespace TFaller.ALTools.Transformation;

public static class AppPackageHelper
{
    public static ModuleDefinition ReadModule(this NavAppPackageReader reader) =>
        SymbolReferenceJsonReader.ReadModule(reader.ReadSymbolReferenceFile());

    public static void WriteModule(this NavAppPackage app, ModuleDefinition moduleDefinition)
    {
        using var writer = new NavAppPackageWriter(app, FileMode.Open);
        using var stream = writer.GetPartStream("/SymbolReference.json");
        SymbolReferenceJsonWriter.WriteModule(stream, moduleDefinition);
    }
}