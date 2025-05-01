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
    }
}