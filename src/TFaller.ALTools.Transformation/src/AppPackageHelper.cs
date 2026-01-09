using Microsoft.Dynamics.Nav.CodeAnalysis.Packaging;
using Microsoft.Dynamics.Nav.CodeAnalysis.SymbolReference;
using System;
using System.IO;

namespace TFaller.ALTools.Transformation;

public static class AppPackageHelper
{
    public static ModuleDefinition ReadModule(this NavAppPackageReader reader) =>
        SymbolReferenceJsonReader.ReadModule(reader.ReadSymbolReferenceFile());

    public static void WriteModule(this NavAppPackage app, ModuleDefinition moduleDefinition)
    {
        using var writer = new NavAppPackageWriter(app, FileMode.Open);
        writer.WriteModule(moduleDefinition);
    }

    public static void WriteModule(this NavAppPackageWriter writer, ModuleDefinition moduleDefinition)
    {
        using var stream = writer.GetPartStream("/SymbolReference.json");
        SymbolReferenceJsonWriter.WriteModule(stream, moduleDefinition);
    }

    public static void WriteManifest(this NavAppPackageWriter writer, NavAppManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest, nameof(manifest));

        // The following checks make sure we write a manifest that actually is valid and can be read by other tools

        ArgumentNullException.ThrowIfNull(manifest.AppVersion, nameof(manifest) + "." + nameof(manifest.AppVersion));

        if (manifest.AppId == default)
        {
            // Technically other tools might accept this, but it could break if multiple apps use the empty GUID
            throw new ArgumentException("AppId must be set in the manifest before writing it to a package.", nameof(manifest));
        }

        if (manifest.AppCompatibilityId == null)
        {
            // Don't throw here, this is kind of a special case. Use the default version
            manifest.AppCompatibilityId = new Version(0, 0, 0, 0);
        }

        writer.WriteString(manifest.ToXml(), "/NavxManifest.xml");
    }
}