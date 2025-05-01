using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Models.Interfaces;
using Microsoft.OpenApi.Models.References;
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
        foreach (var schema in document.Components!.Schemas!.ToArray())
        {
            TransformNested(document, schema.Key, schema.Value);
        }
    }

    private static void TransformNested(OpenApiDocument document, string objectPath, IOpenApiSchema schema)
    {
        if (schema.Type == JsonSchemaType.Array && schema is OpenApiSchema array)
        {
            TransformNestedArray(document, objectPath + "Item", array);
            return;
        }

        if (schema.Type != JsonSchemaType.Object || schema.Properties is null)
        {
            return;
        }

        foreach (var prop in schema.Properties)
        {
            var ps = prop.Value;

            if (ps.Type == JsonSchemaType.Array && ps is OpenApiSchema propArry)
            {
                TransformNestedArray(document, objectPath + prop.Key.ToPascalCase(false), propArry);
            }
            else if (ps?.Type == JsonSchemaType.Object && ps is not OpenApiSchemaReference)
            {
                var name = objectPath + prop.Key.ToPascalCase(false);

                document.AddComponent(name, ps);
                schema.Properties[prop.Key] = new OpenApiSchemaReference(name, document);

                TransformNested(document, name, ps);
            }
        }
    }

    private static void TransformNestedArray(OpenApiDocument document, string objectPath, OpenApiSchema schema)
    {
        var items = schema.Items;

        if (items?.Type != JsonSchemaType.Object || items is OpenApiSchemaReference)
        {
            return;
        }

        document.AddComponent(objectPath, items);
        schema.Items = new OpenApiSchemaReference(objectPath, document);

        TransformNested(document, objectPath, items);
    }
}