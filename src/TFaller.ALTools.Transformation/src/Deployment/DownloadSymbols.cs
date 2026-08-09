using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Dynamics.Nav.Deployment;

namespace TFaller.ALTools.Transformation.Deployment;

public static class DownloadSymbols
{
    public static async Task DownloadSymbolsAsync(string workspace, string connectionUri)
    {
        var uri = new Uri(connectionUri);
        var options = ConnectionOptionsFromUri(uri);

        var userInfosParts = uri.UserInfo.Split(':', 2);
        if (userInfosParts.Length != 2)
        {
            throw new ArgumentException("Connection URI must contain username and password");
        }

        var downloader = new NavDevServerPackageDownloader(options, new DownloadSymbolsLogger());
        OnPremiseHttpClientFactory.AddCredential(options, AuthenticationMethod.UserPassword, HttpUtility.UrlDecode(userInfosParts[0]), HttpUtility.UrlDecode(userInfosParts[1]));

        var manifest = await WorkspaceHelper.LoadAppManifestAsync(workspace);
        var references = manifest.GetAllReferences().ToImmutableArray();

        await downloader.DownloadPackages(references, workspace + "/.alpackages");
    }

    public static ConnectionOptions ConnectionOptionsFromUri(Uri uri)
    {
        var queryParams = HttpUtility.ParseQueryString(uri.Query);

        return new ConnectionOptions
        {
            ValidateServerCertificate = (queryParams["validateServerCertificate"]?.ToLower() ?? "true") == "true",
            Authentication = AuthenticationMethod.UserPassword,
            Tenant = queryParams["tenant"] ?? "default",
            Server = uri.Scheme + "://" + uri.Host,
            ServerInstance = uri.AbsolutePath.TrimStart('/'),
            Port = uri.Port,
        };
    }

    internal class DownloadSymbolsLogger : IEmitLogger
    {
        public void Error(string message, params object[] args)
        {
            Console.WriteLine(message, args);
        }

        public void Error(string message)
        {
            Console.WriteLine(message);
        }

        public void Exception(Exception ex)
        {
            throw ex;
        }

        public void Info(string message, params object[] args)
        {
            Console.WriteLine(message, args);
        }

        public void NetworkException(Exception ex)
        {
            throw ex;
        }

        public void OpenUri(string uri)
        {
            throw new NotImplementedException();
        }

        public void ShowDeviceLoginDialog(string message, string uri, string token)
        {
            throw new NotImplementedException();
        }
    }
}