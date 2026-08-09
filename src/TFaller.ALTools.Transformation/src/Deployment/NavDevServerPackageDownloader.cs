using System;
using System.Collections.Immutable;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.Deployment;

namespace TFaller.ALTools.Transformation.Deployment;

public class NavDevServerPackageDownloader(ConnectionOptions options, IEmitLogger logger)
{
    private static readonly Type DownloaderType = DeploymentAssembly.Assembly.GetType("Microsoft.Dynamics.Nav.Deployment.ReferenceDownloader.NavDevServerPackageDownloader") ??
            throw new InvalidOperationException("NavDevServerPackageDownloader type not found.");
    private static readonly MethodInfo DownloadPackagesMethod = DownloaderType.GetMethod("DownloadPackages") ??
            throw new InvalidOperationException("DownloadPackages method not found.");
    private readonly object _downloaderHandler = Activator.CreateInstance(DownloaderType, [options, logger])
            ?? throw new InvalidOperationException("Could not create downloader instance.");

    public Task DownloadPackages(ImmutableArray<SymbolReferenceSpecification> references, string outputPath)
    {
        return (Task?)DownloadPackagesMethod.Invoke(_downloaderHandler, [references, outputPath])
            ?? throw new InvalidOperationException("Could not invoke DownloadPackages method.");
    }
}