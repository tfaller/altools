using Microsoft.OpenApi;
using System;

namespace TFaller.ALTools.OpenApiGenerator;

public class PropertyGenerationException(string message, string name, IOpenApiSchema schema) : InvalidOperationException(
    string.Format(
        "{0}: for property '{1}' with schema: {2}",
        message, name, schema.SerializeAsYamlAsync(OpenApiSpecVersion.OpenApi3_2).Result
    )
)
{
}