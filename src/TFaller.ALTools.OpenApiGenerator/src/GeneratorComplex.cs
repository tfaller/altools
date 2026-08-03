using Microsoft.OpenApi;
using System.Text;
using TFaller.ALTools.Transformation;

namespace TFaller.ALTools.OpenApiGenerator;

public class GeneratorComplex(Generator generator) : IGenerator
{
    private readonly Generator _generator = generator;

    public GenerationStatus GenerateCode(StringBuilder code, string name, IOpenApiSchema schema, bool required)
    {
        if (schema.Type != JsonSchemaType.Object && schema.Type != JsonSchemaType.Array)
            return GenerationStatus.Nothing;

        var alName = _generator.ALName(name);

        var type = _generator.ALObjectName(
            schema.Type == JsonSchemaType.Object
            ? ((OpenApiSchemaReference)schema).Reference.Id!
            : _generator.ArrayTypeMapper(schema) + "Array"
        );

        CreateGetterCode(code, name, alName, type);
        CreateSetterCode(code, name, alName, type);

        var status = GenerationStatus.Getter | GenerationStatus.Setter;

        if (_generator.GenerateValidate)
        {
            CreateValidateCode(code, name, alName, type, schema, required);
            status |= GenerationStatus.Validate;
        }

        return status;
    }

    private static void CreateGetterCode(StringBuilder code, string name, string alName, string type)
    {
        code.Append($@"
            procedure Get{alName}(var {alName}: Codeunit {type})
            var 
                NewObj: Codeunit {type};
                Token: JsonToken;
            begin
                J.Get('{name}', Token);
                NewObj.FromJson(Token);
                {alName} := NewObj;
            end;
        ");
    }

    private static void CreateSetterCode(StringBuilder code, string name, string alName, string type)
    {
        code.Append($@"
            procedure Set{alName}(var {alName}: Codeunit {type})
            var
                Token: JsonToken;
            begin
                Token := {alName}.AsJson();
                if J.Contains('{name}') then
                    J.Replace('{name}', Token)
                else
                    J.Add('{name}', Token);
            end;
        ");
    }

    public static void CreateValidateCode(StringBuilder code, string name, string alName, string type, IOpenApiSchema schema, bool required)
    {
        code.AppendLine($@"
            procedure {Formatter.CombineIdentifiers("Validate", alName)}(Path: Text) Error: Text
            var Token: JsonToken;
                Obj: Codeunit {type};
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

        if (schema.Type == JsonSchemaType.Array)
        {
            if (schema.MaxItems != null)
            {
                code.AppendLine($@"if Token.AsArray().Count() > {schema.MaxItems} then 
                    exit(Path + '.{name} count value is greater than max items');
                ");
            }

            if (schema.MinItems != null)
            {
                code.AppendLine($@"if Token.AsArray().Count() < {schema.MinItems} then 
                    exit(Path + '.{name} count value is less than min items');
                ");
            }
        }

        // validate the prop type itself
        code.AppendLine($@"Get{alName}(Obj);");
        code.AppendLine($@"Error := Obj.Validate(Path + '.{name}');");
        code.AppendLine("if Error <> '' then exit(Error);");

        code.AppendLine("end;");
    }
}