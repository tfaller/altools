using Microsoft.OpenApi.Models;
using Microsoft.Dynamics.Nav.CodeAnalysis.Utilities;

namespace TFaller.ALTools.OpenApiGenerator;

public class TransformInlineTypes
{
    /// <summary>
    /// Transforms inline schemas to referenced schemas
    /// </summary>
    /// <param name="document"></param>
    public static void Transform(OpenApiDocument document)
    {
        foreach (var schema in document.Components.Schemas)
        {
            TransformNested(document, schema.Key, schema.Value);
        }
    }

    private static void TransformNested(OpenApiDocument document, string objectPath, OpenApiSchema schema)
    {
        if (schema.Type != "object")
        {
            return;
        }

        foreach (var prop in schema.Properties)
        {
            var ps = prop.Value;

            if (ps.Type == "array")
            {
                ps = ps.Items;
            }

            if (ps.Type == "object" && ps.Reference == null)
            {
                var name = objectPath + prop.Key.ToPascalCase(false);

                ps.Reference = new OpenApiReference()
                {
                    Type = ReferenceType.Schema,
                    Id = name
                };

                document.Components.Schemas.Add(name, ps);

                TransformNested(document, name, ps);
            }
        }
    }
}