using Microsoft.Dynamics.Nav.Deployment;
using System;
using TFaller.ALTools.Transformation.Deployment;

namespace TFaller.ALTools.Transformation.Tests;

public class DownloadSymbolsTest
{
    public static TheoryData<string, ConnectionOptions> ConnectionOptionsFromUriData =>
        new()
        {
            {
                "https://bc.example.com:8049/Instance?tenant=nonDefault",
                new ConnectionOptions
                {
                    Server = "https://bc.example.com",
                    ServerInstance = "Instance",
                    Port = 8049,
                    Authentication = AuthenticationMethod.UserPassword,
                    Tenant = "nonDefault"
                }
            },
            {
                "https://bc.example.com:8049/Instance",
                new ConnectionOptions
                {
                    Server = "https://bc.example.com",
                    ServerInstance = "Instance",
                    Port = 8049,
                    Authentication = AuthenticationMethod.UserPassword,
                    Tenant = "default"
                }
            }
        };

    [Theory]
    [MemberData(nameof(ConnectionOptionsFromUriData))]
    public void TestConnectionOptionsFromUri(string uri, ConnectionOptions expected)
    {
        var result = DownloadSymbols.ConnectionOptionsFromUri(new Uri(uri));
        Assert.Equal(expected, result);
    }
}