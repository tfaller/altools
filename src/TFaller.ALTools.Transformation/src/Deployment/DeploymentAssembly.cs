using System;
using System.Reflection;
using Microsoft.Dynamics.Nav.Deployment;

namespace TFaller.ALTools.Transformation.Deployment;

public static class DeploymentAssembly
{
    public static readonly Assembly Assembly = typeof(ConnectionOptions).Assembly ??
        throw new InvalidOperationException("Deployment assembly not found.");
}