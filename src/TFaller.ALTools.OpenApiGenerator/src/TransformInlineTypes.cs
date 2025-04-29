using Microsoft.OpenApi.Models;
using Microsoft.Dynamics.Nav.CodeAnalysis.Utilities;
using System.Linq;

namespace TFaller.ALTools.OpenApiGenerator;

public class TransformInlineTypes
{
    /// <summary>
    /// Transforms inline schemas to referenced schemas
    /// </summary>
    /// <param name="document"></param>
    public static void Transform(OpenApiDocument document)
    {
        // toArray(), because we are adding to the collection while iterating over it
        foreach (var schema in document.Components.Schemas.ToArray())
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