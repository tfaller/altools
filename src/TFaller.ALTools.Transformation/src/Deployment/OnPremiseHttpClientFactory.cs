using System;
using System.Collections;
using System.Reflection;
using Microsoft.Dynamics.Nav.Deployment;

namespace TFaller.ALTools.Transformation.Deployment;

public static class OnPremiseHttpClientFactory
{
    private static readonly IDictionary CredentialsCache;
    private static readonly Type UsernamePasswordType;
    private static readonly Type AuthenticationMethodUsernamePasswordTupelType;

    static OnPremiseHttpClientFactory()
    {
        var onPremiseHttpClientFactoryType = DeploymentAssembly.Assembly.GetType("Microsoft.Dynamics.Nav.Deployment.Http.OnPremiseHttpClientFactory") ??
            throw new InvalidOperationException("OnPremiseHttpClientFactory type not found.");

        var credentialsCacheField = onPremiseHttpClientFactoryType.GetField("CredentialsCache", BindingFlags.Static | BindingFlags.NonPublic) ??
            throw new InvalidOperationException("CredentialsCache field not found.");

        CredentialsCache = (IDictionary?)credentialsCacheField.GetValue(null) ??
            throw new InvalidOperationException("CredentialsCache is null.");

        UsernamePasswordType = DeploymentAssembly.Assembly.GetType("Microsoft.Dynamics.Nav.Deployment.Authentication.UsernamePassword") ??
            throw new InvalidOperationException("UsernamePassword type not found.");

        AuthenticationMethodUsernamePasswordTupelType = typeof(ValueTuple<,>).MakeGenericType(typeof(AuthenticationMethod), UsernamePasswordType);
    }

    public static void AddCredential(ConnectionOptions options, AuthenticationMethod authMethod, string username, string password)
    {
        var usernamePassword = Activator.CreateInstance(UsernamePasswordType, [username, password]) ??
            throw new InvalidOperationException("Could not create UsernamePassword instance.");

        object credentials = Activator.CreateInstance(AuthenticationMethodUsernamePasswordTupelType, authMethod, usernamePassword) ??
            throw new InvalidOperationException("Could not create tuple instance.");

        CredentialsCache[options.GetCacheKey()] = credentials;
    }
}