using Microsoft.OpenApi.Models;
using static TFaller.ALTools.OpenApiGenerator.Tests.OpenApiHelpers;

namespace TFaller.ALTools.OpenApiGenerator.Tests;

public class TransformInlineTypesTests
{
    [Fact]
    public void TransformArrayObject()
    {
        var doc = ParseYamlOpenApiDocument(
            """
            openapi: 3.1.0
            info:
              title: Test
              version: 1
            components:
              schemas:
                list:
                  type: array
                  items:
                    type: object
            """);

        TransformInlineTypes.Transform(doc);

        var schemas = doc.Components?.Schemas;
        Assert.NotNull(schemas);
        Assert.Contains("list", schemas);
        Assert.Contains("listItem", schemas);
        Assert.Equal(2, schemas.Count);
    }

    [Fact]
    public void TransformObjectArrayObject()
    {
        var doc = ParseYamlOpenApiDocument(
            """
            openapi: 3.1.0
            info:
              title: Test
              version: 1
            components:
              schemas:
                object:
                  type: object
                  properties:
                    list:
                      type: array
                      items:
                        type: object
            """);

        TransformInlineTypes.Transform(doc);

        var schemas = doc.Components?.Schemas;
        Assert.NotNull(schemas);
        Assert.Contains("object", schemas);
        Assert.Contains("objectList", schemas);
        Assert.Equal(2, schemas.Count);
        Assert.Equal(JsonSchemaType.Object, schemas["object"]?.Type);
        Assert.Equal(JsonSchemaType.Array, schemas["object"]?.Properties?["list"].Type);
        Assert.Equal(JsonSchemaType.Object, schemas["objectList"]?.Type);
    }
}