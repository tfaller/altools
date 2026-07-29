using static TFaller.ALTools.OpenApiGenerator.Tests.OpenApiHelpers;

namespace TFaller.ALTools.OpenApiGenerator.Tests;

/// <summary>
/// Regression tests for property names containing special characters such as "@"
/// that previously generated invalid AL procedure names (e.g., Validate"@Context").
/// </summary>
public class GeneratorSpecialCharPropertyTests
{
    private static string GenerateCode(string yaml, bool withValidate = true)
    {
        var doc = ParseYamlOpenApiDocument(yaml);
        var generator = new Generator { GenerateValidate = withValidate };
        generator.AddComponents(doc.Components!);
        return generator.GetCode();
    }

    [Fact]
    public void AtSign()
    {
        var code = GenerateCode(
            """
            openapi: 3.1.0
            info:
              title: Test
              version: 1
            components:
              schemas:
                response:
                  type: object
                  properties:
                    "@context":
                      type: string
            """);

        Assert.DoesNotContain("Validate\"", code);
        Assert.Contains("\"Validate", code);

        Assert.DoesNotContain("Had\"", code);
        Assert.Contains("\"Has", code);

        Assert.DoesNotContain("Remove\"", code);
        Assert.Contains("\"Remove", code);
    }
}
