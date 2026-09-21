using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace TFaller.ALTools.Transformation;

public static class AssemblyLoader
{
    /// <summary>
    /// Assemblies that are loaded/provided from the AL extension
    /// </summary>
    private static readonly HashSet<string> _alAssemblies =
    [
        "DocumentFormat.OpenXml",
        "Microsoft.CodeAnalysis",
        "Microsoft.CodeAnalysis.CSharp",
        "Microsoft.Dynamics.Nav.AL.Common",
        "Microsoft.Dynamics.Nav.CodeAnalysis",
        "Microsoft.Dynamics.Nav.CodeAnalysis.Workspaces",
        "Microsoft.Dynamics.Nav.Deployment",
        "Microsoft.Dynamics.Nav.EditorServices.Protocol",
        // "external" dependencies also provided by the AL extension
        "Microsoft.ApplicationInsights",
        "Newtonsoft.Json",
    ];

    /// <summary>
    /// Analyzer assemblies that are provided from the AL extension in a separate folder
    /// </summary>
    private static readonly HashSet<string> _alAnalyzerAssemblies =
    [
        "Microsoft.Dynamics.Nav.Analyzers.Common",
        "Microsoft.Dynamics.Nav.AppSourceCop",
        "Microsoft.Dynamics.Nav.CodeCop",
        "Microsoft.Dynamics.Nav.PerTenantExtensionCop",
        "Microsoft.Dynamics.Nav.UICop",
    ];

    private static readonly Dictionary<string, Assembly> _loadedAlAssemblies = [];

    public static void RegisterLoader()
    {
        var alExtensionPath = FindAlExtension();
        string? extensionBinPath = null;

        if (alExtensionPath is not null)
        {
            extensionBinPath = Path.Combine(alExtensionPath, "bin");
            if (Directory.Exists(extensionBinPath))
                // must be an absolute path
                extensionBinPath = Path.GetFullPath(extensionBinPath);
        }

        var bcToolsDllsPath = BcToolsDllsPath();

        if (extensionBinPath is null && bcToolsDllsPath is null)
            throw DllsNotFoundException();

        AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) =>
        {
            var name = eventArgs.Name.Split(",", 2)[0];

            var basePath =
                extensionBinPath is null ? bcToolsDllsPath :
                _alAssemblies.Contains(name) ? extensionBinPath :
                _alAnalyzerAssemblies.Contains(name) ? extensionBinPath :
                null;

            if (basePath == null)
                return null;

            lock (_loadedAlAssemblies)
            {
                if (!_loadedAlAssemblies.TryGetValue(name, out var assembly))
                    _loadedAlAssemblies.Add(name, assembly = Assembly.LoadFile(Path.Combine(basePath, name + ".dll")));

                return assembly;
            }
        };
    }

    public static string AnalyzerFullPathByName(string name)
    {
        var alExtensionPath = FindAlExtension();

        if (alExtensionPath is not null)
        {
            return Path.Combine(alExtensionPath, "bin", name + ".dll");
        }

        var bcToolsDllsPath = BcToolsDllsPath();
        if (bcToolsDllsPath is not null)
        {
            return Path.Combine(bcToolsDllsPath, name + ".dll");
        }

        throw DllsNotFoundException();
    }

    private static string? FindAlExtension()
    {
        var vscodeExtensionsPath = FindVscodeExtensionDir();
        if (string.IsNullOrEmpty(vscodeExtensionsPath))
            return null;

        var alExtensions = Directory.GetDirectories(vscodeExtensionsPath, "ms-dynamics-smb.al-*");
        return alExtensions.Max();
    }

    private static string? FindVscodeExtensionDir()
    {
        var vscodePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".vscode");
        var vscodeExtensionsPath = Path.Combine(vscodePath, "extensions");

        if (!Directory.Exists(vscodeExtensionsPath))
            return null;

        return vscodeExtensionsPath;
    }

    private static string? DotNetCliHomePath()
    {
        var cliHome = Environment.GetEnvironmentVariable("DOTNET_CLI_HOME");
        if (!string.IsNullOrEmpty(cliHome))
            return cliHome;

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile))
            return userProfile;

        return null;
    }

    private static string? NugetPackagesPath()
    {
        var nugetPackages = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrEmpty(nugetPackages))
            return nugetPackages;

        var dotNetCliHome = DotNetCliHomePath();
        if (!string.IsNullOrEmpty(dotNetCliHome))
            return Path.Combine(dotNetCliHome, ".nuget", "packages");

        return null;
    }

    private static string? BcToolsDllsPath()
    {
        var nugetPackages = NugetPackagesPath();
        if (string.IsNullOrEmpty(nugetPackages))
            return null;

        var bcToolsPath = Path.Combine(nugetPackages, "microsoft.dynamics.businesscentral.development.tools");
        if (!Directory.Exists(bcToolsPath))
            return null;

        var bcToolsVersions = Directory.GetDirectories(bcToolsPath);
        if (bcToolsVersions.Length == 0)
            return null;

        var latestBcToolsVersion = bcToolsVersions.Max()!;
        var bcToolsDllsPath = Path.Combine(latestBcToolsVersion, "tools", "net10.0", "any");

        if (!Directory.Exists(bcToolsDllsPath))
            return null;

        return bcToolsDllsPath;
    }

    private static DirectoryNotFoundException DllsNotFoundException()
    {
        return new DirectoryNotFoundException("Could not find needed AL Dlls. Either install the AL vscode extension or install the Microsoft.Dynamics.BusinessCentral.Development.Tools as a local tool");
    }
}