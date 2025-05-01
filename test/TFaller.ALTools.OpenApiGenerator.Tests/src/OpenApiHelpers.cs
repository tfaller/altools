using Microsoft.OpenApi.Interfaces;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Reader;
using Microsoft.OpenApi.Readers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TFaller.ALTools.OpenApiGenerator.Tests;

public static class OpenApiHelpers
{
    public static OpenApiDocument ParseYamlOpenApiDocument(string yaml)
    {
        var settings = new OpenApiReaderSettings
        {
            Readers = new Dictionary<string, IOpenApiReader>
            {
                { OpenApiConstants.Yaml, new OpenApiYamlReader() }
            }
        };

        var result = OpenApiDocument.Parse(yaml, OpenApiConstants.Yaml, settings);
        var diagnostic = result.Diagnostic;

        if (diagnostic?.Errors.Count > 0)
        {
            throw new InvalidOperationException(
                    $"Errors parsing OpenAPI document: {string.Join(", ", diagnostic.Errors.Select(e => e.ToString()))}");
        }

        return result.Document ?? throw new InvalidOperationException("parsed document is null, but no errors were reported");
    }
}