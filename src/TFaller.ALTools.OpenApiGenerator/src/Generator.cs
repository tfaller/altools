using Microsoft.Dynamics.Nav.CodeAnalysis.Utilities;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Text;
using TFaller.ALTools.Transformation;

namespace TFaller.ALTools.OpenApiGenerator;

public class Generator
{
    public static readonly StringComparison ALStringComparison = StringComparison.InvariantCultureIgnoreCase;
    private int _nextCodeunitId;
    private readonly HashSet<int> _existingCodeunitIds = [];
    private readonly StringBuilder _code = new();
    private readonly List<IGenerator> _generators;
    private readonly IdentifierDictionary<string> _arrayTypes = [];
    private static readonly HashSet<string> _keywords = new(StringComparer.InvariantCultureIgnoreCase)
    {
        "end",
        "to",
    };
    private static readonly HashSet<string> _arraySupportedTypes = new(StringComparer.InvariantCultureIgnoreCase)
    {
        "object",
        "string"
    };

    public bool GenerateValidate { get; set; }

    public Generator()
    {
        _generators = [
            new GeneratorPrimitive(this),
            new GeneratorComplex(this),
            new GeneratorHas(this),
        ];
    }

    public string GetCode()
    {
        return _code.ToString();
    }

    public void AddComponents(OpenApiComponents components)
    {
        foreach (var s in components.Schemas)
        {
            if (s.Value.Type == "object")
            {
                AddObjectSchema(s.Key, s.Value);
            }

            if (s.Value.Type == "array")
            {
                AddArraySchema(s.Value);
            }
        }
    }

    private void AddObjectSchema(string name, OpenApiSchema schema)
    {
        if (schema.Type != "object")
            throw new ArgumentException("schema must be an object");

        var code = _code;

        // basic object header

        code.AppendLine($@"
            Codeunit {GetFreeCodeunitId()} {name} {{
            Var J: JsonObject;

            procedure FromJson(Json: JsonToken) begin
                J := Json.AsObject()
            end;

            procedure AsJson(): JsonToken begin
                exit(J.AsToken());
            end;

            procedure AsJsonObject(): JsonObject
            begin
                exit(J);
            end;
        ");

        // object properties

        var arrays = new HashSet<OpenApiSchema>();
        var allProps = new HashSet<string>();
        var validateProps = new IdentifierDictionary<bool>();

        foreach (var p in schema.Properties)
        {
            var prop = p;
            var propRequired = schema.Required.Contains(prop.Key);

            allProps.Add(prop.Key);

            if (prop.Value.Type == "array")
            {
                var type = prop.Value.Items.Type;

                if (!_arraySupportedTypes.Contains(type))
                {
                    continue;
                }

                type = ArrayTypeMapper(prop.Value);

                arrays.Add(prop.Value);
                prop = new KeyValuePair<string, OpenApiSchema>(prop.Key, new OpenApiSchema
                {
                    Type = "object",
                    Reference = new OpenApiReference
                    {
                        Id = ALName(type) + "Array"
                    },
                    MinItems = prop.Value.MinItems,
                    MaxItems = prop.Value.MaxItems,
                });
            }

            if (prop.Value.AnyOf.Count > 0)
            {
                // Not supported currently
                continue;
            }

            var status = GenerationStatus.Nothing;

            foreach (var generator in _generators)
            {
                var result = generator.GenerateCode(_code, prop.Key, prop.Value, propRequired);

                if ((result & status) != GenerationStatus.Nothing)
                {
                    throw new PropertyGenerationException("Duplicate code generated", prop.Key, prop.Value);
                }

                status |= result;
            }

            if (status == GenerationStatus.Nothing)
            {
                throw new PropertyGenerationException("No code was generated", prop.Key, prop.Value);
            }

            if ((status & GenerationStatus.Validate) == GenerationStatus.Validate)
            {
                validateProps.Add(prop.Key, true);
            }
            else if (GenerateValidate)
            {
                throw new PropertyGenerationException("No validation code was generated", prop.Key, prop.Value);
            }

            if (!propRequired && (status & GenerationStatus.Has) == 0)
            {
                throw new PropertyGenerationException("No 'Has' code was generated", prop.Key, prop.Value);
            }
        }

        if (GenerateValidate)
        {
            code.AppendLine($@"
                procedure Validate(): Text
                begin
                    exit(Validate('$'));
                end;

                procedure Validate(Path: Text): Text
                var
                    PropKey: Text;
                    Error: Text;
                begin
            ");

            foreach (var p in validateProps)
            {
                code.AppendLine($@"Error := Validate{ALName(p.Key)}(Path);");
                code.AppendLine("if Error <> '' then exit(Error);");
            }

            if (!schema.AdditionalPropertiesAllowed)
            {
                code.AppendLine("foreach PropKey in J.Keys() do begin");

                if (allProps.Count > 0)
                {
                    code.AppendLine("if not(PropKey in [");

                    foreach (var p in allProps)
                    {
                        code.Append($"'{p}',");
                    }
                    code.Length--;

                    code.AppendLine("]) then");
                }

                code.AppendLine("exit(Path + ': Unknown additional property: ' + PropKey);");
                code.AppendLine("end;");
            }

            code.AppendLine("exit('');");
            code.AppendLine("end;");
        }

        code.AppendLine("}");

        foreach (var array in arrays)
        {
            AddArraySchema(array);
        }
    }

    private void AddArraySchema(OpenApiSchema schema)
    {
        if (schema.Type != "array")
            throw new ArgumentException("schema must be an array");

        var type = schema.Items.Type;

        if (type == "array")
        {
            throw new ArgumentException("nested arrays are not supported");
        }

        if (type != "object")
        {
            AddPrimitiveArraySchema(schema);
            return;
        }

        type = ALName(schema.Items.Reference.Id);

        if (_arrayTypes.ContainsKey(type))
        {
            return;
        }
        _arrayTypes.Add(type, type);

        var code = _code;
        code.AppendLine($@"
            Codeunit {GetFreeCodeunitId()} {type}Array {{
            Var J: JsonArray;

            procedure FromJson(Json: JsonToken) begin
                J := Json.AsArray()
            end;

            procedure AsJson(): JsonToken begin
                exit(J.AsToken());
            end;

            procedure AsJsonArray(): JsonArray begin
                exit(J);
            end;

            procedure Add(Item: Codeunit {type}) begin
                J.Add(Item.AsJson());
            end;

            procedure Insert(Index: Integer; Item: Codeunit {type}) begin
                J.Insert(Index, Item.AsJson());
            end;

            procedure Get(Index: Integer; var Item: Codeunit {type}) 
            var
                ItemToken: JsonToken;
                I: Codeunit {type};
            begin
                J.Get(Index, ItemToken);
                I.FromJson(ItemToken);
                Item := I;
            end;

            procedure Set(Index: Integer; Item: Codeunit {type}) begin
                J.Set(Index, Item.AsJson());
            end;

            procedure RemoveAt(Index: Integer) begin
                J.RemoveAt(Index);
            end;

            procedure Count(): Integer
            begin
                exit(J.Count());
            end;
            ");

        if (GenerateValidate)
        {
            code.AppendLine($@"
                procedure Validate(Path: Text) Err: Text
                var
                    I: Integer;
                    Item: Codeunit {type};
                begin
                    for I := 0 to J.Count() - 1 do begin
                        Get(I, Item);
                        Err := Item.Validate(Path + '[' + Format(I) + ']');
                        if Err <> '' then exit(Err);
                    end;
                end;
            ");
        }

        code.AppendLine($@"}}");
    }

    private string ArrayTypeMapper(OpenApiSchema schema)
    {
        if (schema.Type != "array")
            throw new ArgumentException("schema must be an array");

        return schema.Items.Type switch
        {
            "string" => "Text",
            "object" => ALName(schema.Items.Reference.Id),
            _ => throw new ArgumentException(string.Format("schame has unsupporetd typ {0}", schema.Items.Type)),
        };
    }

    private void AddPrimitiveArraySchema(OpenApiSchema schema)
    {
        if (schema.Type != "array")
            throw new ArgumentException("schema must be an array");

        var type = schema.Items.Type;

        switch (type)
        {
            case "string":
                type = "Text";
                break;

            default:
                // not a primitive type
                return;
        }

        type = ALName(type);

        if (_arrayTypes.ContainsKey(type))
        {
            return;
        }
        _arrayTypes.Add(type, type);

        var code = _code;
        code.AppendLine($@"
            Codeunit {GetFreeCodeunitId()} {type}Array {{
            Var J: JsonArray;

            procedure FromJson(Json: JsonToken) begin
                J := Json.AsArray()
            end;

            procedure AsJson(): JsonToken begin
                exit(J.AsToken());
            end;

            procedure AsJsonArray(): JsonArray begin
                exit(J);
            end;

            procedure Add(Item: {type}) begin
                J.Add(Item);
            end;

            procedure Insert(Index: Integer; Item: {type}) begin
                J.Insert(Index, Item);
            end;

            procedure Get(Index: Integer): {type} 
            var
                ItemToken: JsonToken;
            begin
                J.Get(Index, ItemToken);
                exit(ItemToken.AsValue().As{type}());
            end;

            procedure Set(Index: Integer; Item: {type}) begin
                J.Set(Index, Item);
            end;

            procedure RemoveAt(Index: Integer) begin
                J.RemoveAt(Index);
            end;

            procedure Count(): Integer
            begin
                exit(J.Count());
            end;
        ");

        code.AppendLine($@"}}");
    }

    private int GetFreeCodeunitId()
    {
        while (_existingCodeunitIds!.Contains(_nextCodeunitId))
            _nextCodeunitId++;

        _existingCodeunitIds.Add(_nextCodeunitId);
        return _nextCodeunitId++;
    }

    /// <summary>
    /// Generates a valid AL name. E.g. for procedures, variables, parameters, object names
    /// </summary>
    /// <param name="name">The name which should be converted</param>
    /// <returns>Valid AL Name</returns>
    public string ALName(string name)
    {
        name = name.ToPascalCase(false)
            .Replace(" ", "")
            .Replace("&", "")
            .Replace(".", "_")
            .Replace(",", "_");

        if (name.StartsWith("Get", ALStringComparison) ||
            name.StartsWith("Set", ALStringComparison) ||
            name.StartsWith("Has", ALStringComparison) ||
            name.StartsWith("Remove", ALStringComparison) ||
            name.StartsWith("Property", ALStringComparison) ||
            name.StartsWith("FromJson", ALStringComparison) ||
            name.StartsWith("AsJson", ALStringComparison) ||
            name.StartsWith("AsJsonObject", ALStringComparison) ||
            name.StartsWith("AsJsonArray", ALStringComparison) ||
            name.StartsWith("Validate", ALStringComparison) ||
            _keywords.Contains(name))
        {
            return "Property" + name;
        }

        return name;
    }
}