using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace TFaller.ALTools.Transformation;

public static class AssemblyLoader
{
    private static readonly HashSet<string> _alAssemblies =
    [
        "DocumentFormat.OpenXml",
        "Microsoft.Dynamics.Nav.AL.Common",
        "Microsoft.Dynamics.Nav.CodeAnalysis",
    ];

    private static readonly Dictionary<string, Assembly> _loadedAlAssemblies = [];

    public static void RegisterLoader()
    {
        var alExtensionPath = FindAlExtension();
        if (alExtensionPath == null)
            throw new DirectoryNotFoundException("Could not find AL extension");

        var binPath = Path.Combine(alExtensionPath, "bin", OsPath());
        if (!Directory.Exists(binPath))
            throw new DirectoryNotFoundException($"Could not find bin directory at {binPath}");

        AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) =>
        {
            var name = eventArgs.Name.Split(",", 2)[0];

            if (!_alAssemblies.Contains(name))
                return null;

            if (_loadedAlAssemblies.TryGetValue(name, out var assembly))
                return assembly;

            _loadedAlAssemblies.Add(name, assembly = Assembly.LoadFile(Path.Combine(binPath, name + ".dll")));
            return assembly;
        };
    }

    private static string? FindAlExtension()
    {
        var vscodeExtensionsPath = FindVscodeExtensionDir();
        var alExtensions = Directory.GetDirectories(vscodeExtensionsPath, "ms-dynamics-smb.al-*");
        return alExtensions.Max();
    }

    private static string FindVscodeExtensionDir()
    {
        var vscodePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".vscode");
        var vscodeExtensionsPath = Path.Combine(vscodePath, "extensions");

        if (!Directory.Exists(vscodeExtensionsPath))
            throw new DirectoryNotFoundException($"Could not find vscode extensions directory at {vscodeExtensionsPath}");

        return vscodeExtensionsPath;
    }

    private static string OsPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "win32";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "linux";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "darwin";

        throw new PlatformNotSupportedException();
    }
}