using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace TFaller.ALTools.OpenApiGenerator;

public class GeneratorPrimitive(Generator generator) : IGenerator
{
    public static readonly HashSet<string> SupportedTypes = [
        "boolean",
        "integer",
        "string",
        "number"
    ];

    private readonly Generator _generator = generator;

    public GenerationStatus GenerateCode(StringBuilder code, string name, OpenApiSchema schema, bool required)
    {
        if (!SupportedTypes.Contains(schema.Type))
            return GenerationStatus.Nothing;

        var status = GenerationStatus.Getter | GenerationStatus.Setter;

        CreateGetterCode(code, name, schema);
        CreateSetterCode(code, name, schema);

        if (_generator.GenerateValidate)
        {
            CreateValidateCode(code, name, schema, required);
            status |= GenerationStatus.Validate;
        }

        return status;
    }

    private void CreateGetterCode(StringBuilder code, string name, OpenApiSchema schema)
    {
        var alName = _generator.ALName(name);
        var type = GetALTypeDefintionBySchema(schema);

        code.Append($@"
            procedure {alName}(): {type}
            var Token: JsonToken;
            begin
                J.Get('{name}', Token);
                exit(Token.AsValue().As{type}());
            end;
        ");

        if (schema.Enum.Count > 0)
        {
            foreach (var value in schema.Enum)
            {
                var v = EnumLiteral(value);
                var k = _generator.ALName(v.Trim('\''));

                code.AppendLine($@"
                    procedure Enum{alName}{k}(): {type}
                    begin
                        exit({v});
                    end;");
            }
        }
    }

    private void CreateSetterCode(StringBuilder code, string name, OpenApiSchema schema)
    {
        var alName = _generator.ALName(name);
        var type = GetALTypeDefintionBySchema(schema);

        code.Append($@"
            procedure {alName}({alName}: {type})
            begin
                if J.Contains('{name}') then
                    J.Replace('{name}', {alName})
                else
                    J.Add('{name}', {alName});
            end;
        ");
    }

    public void CreateValidateCode(StringBuilder code, string name, OpenApiSchema schema, bool required)
    {
        var alName = _generator.ALName(name);
        var type = GetALTypeDefintionBySchema(schema);

        code.AppendLine($@"
            procedure Validate{alName}(Path: Text): Text
            var Token: JsonToken;
            begin
        ");

        // basic value check 

        code.AppendLine($@"if not J.Contains('{name}') then");

        if (required)
        {
            code.AppendLine($@"exit(Path + '.{name} is required');");
        }
        else
        {
            code.AppendLine($@"exit('');");
        }

        code.AppendLine($@"J.Get('{name}', Token);");

        if (schema.Enum.Count > 0)
        {
            code.Append($@"if not (Token.AsValue().As{type}() in [");

            foreach (var value in schema.Enum)
            {
                code.Append(EnumLiteral(value));
                code.Append(',');
            }
            code.Length--;

            code.AppendLine($@"]) then 
                    exit(Path + '.{name} value is not in enum');
                ");
        }

        code.AppendLine($@"end;");
    }

    private string GetALTypeDefintionBySchema(OpenApiSchema schema)
    {
        switch (schema.Type)
        {
            case "string":
                return "Text";
            case "integer":
                return "Integer";
            case "boolean":
                return "Boolean";
            case "number":
                return "Decimal";
        }
        throw new ArgumentException(string.Format("schame has unsupporetd typ {0}", schema.Type));
    }

    private string EnumLiteral(IOpenApiAny value)
    {
        if (value is OpenApiString str)
        {
            return $"'{str.Value}'";
        }
        if (value is OpenApiBoolean boolean)
        {
            return boolean.Value ? "true" : "false";
        }
        if (value is OpenApiInteger integer)
        {
            return integer.Value.ToString();
        }
        if (value is OpenApiLong longValue)
        {
            return longValue.Value.ToString();
        }
        if (value is OpenApiDouble number)
        {
            return number.Value.ToString();
        }
        if (value is OpenApiFloat floatNumber)
        {
            return floatNumber.Value.ToString();
        }
        else
        {
            throw new ArgumentException(string.Format("enum value has unsupporetd type {0}", value.GetType()));
        }
    }
}