using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Models.Interfaces;
using System;

namespace TFaller.ALTools.OpenApiGenerator;

public class PropertyGenerationException(string message, string name, IOpenApiSchema schema) : InvalidOperationException(
    string.Format(
        "{0}: for property '{1}' with schema: {2}",
        message, name, schema.SerializeAsYamlAsync(Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_1).Result
    )
)
{
}