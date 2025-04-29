using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Models;
using System;

namespace TFaller.ALTools.OpenApiGenerator;

public class PropertyGenerationException(string message, string name, OpenApiSchema schema) : InvalidOperationException(
    string.Format(
        "{0}: for property '{1}' with schema: {2}",
        message, name, schema.SerializeAsYaml(Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0)
    )
)
{
}