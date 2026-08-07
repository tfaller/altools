using Microsoft.Dynamics.Nav.Deployment;
using TFaller.ALTools.Transformation.Deployment;

namespace TFaller.ALTools.Transformation.Tests;

public class DeploymentAssemblyTest
{
    [Fact]
    public void AssemblyTypeLoaded()
    {
        var type = DeploymentAssembly.Assembly;
        Assert.NotNull(type);
        Assert.Equal(typeof(ConnectionOptions).Assembly, type);
    }
}