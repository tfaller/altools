using Microsoft.Dynamics.Nav.CodeAnalysis.Utilities;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Models.Interfaces;
using Microsoft.OpenApi.Models.References;
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
    private static readonly HashSet<JsonSchemaType?> _arraySupportedTypes =
    [
        JsonSchemaType.Object,
        JsonSchemaType.String,
    ];

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
        foreach (var s in components.Schemas!)
        {
            if (s.Value.Type == JsonSchemaType.Object)
            {
                AddObjectSchema(s.Key, s.Value);
            }

            if (s.Value.Type == JsonSchemaType.Array)
            {
                AddArraySchema(s.Value);
            }
        }
    }

    private void AddObjectSchema(string name, IOpenApiSchema schema)
    {
        if (schema.Type != JsonSchemaType.Object)
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

        var arrays = new HashSet<IOpenApiSchema>();
        var validateProps = new IdentifierDictionary<bool>();

        foreach (var prop in schema.Properties!)
        {
            var propSchema = prop.Value;
            var propRequired = schema.Required?.Contains(prop.Key) ?? false;

            if (propSchema.Type == JsonSchemaType.Array)
            {
                var type = propSchema.Items?.Type;

                if (!_arraySupportedTypes.Contains(type))
                {
                    continue;
                }

                arrays.Add(prop.Value);
            }

            if (propSchema.AnyOf?.Count > 0)
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

                if (schema.Properties.Count > 0)
                {
                    code.AppendLine("if not(PropKey in [");

                    foreach (var p in schema.Properties.Keys)
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

    private void AddArraySchema(IOpenApiSchema schema)
    {
        if (schema.Type != JsonSchemaType.Array)
            throw new ArgumentException("schema must be an array");

        var items = schema.Items ?? throw new ArgumentException("schema must have items");
        if (items.Type == JsonSchemaType.Array)
        {
            throw new ArgumentException("nested arrays are not supported");
        }

        if (items.Type != JsonSchemaType.Object)
        {
            AddPrimitiveArraySchema(schema);
            return;
        }

        var type = ArrayTypeMapper(schema);

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

    public string ArrayTypeMapper(IOpenApiSchema schema)
    {
        if (schema.Type != JsonSchemaType.Array)
            throw new ArgumentException("schema must be an array");

        var items = schema.Items ?? throw new ArgumentException("schema must have items");

        return items.Type switch
        {
            JsonSchemaType.String => "Text",
            JsonSchemaType.Object => ALName(((OpenApiSchemaReference)items).Reference.Id!),
            _ => throw new ArgumentException(string.Format("schame has unsupporetd typ {0}", schema.Items.Type)),
        };
    }

    private void AddPrimitiveArraySchema(IOpenApiSchema schema)
    {
        if (schema.Type != JsonSchemaType.Array)
            throw new ArgumentException("schema must be an array");

        var items = schema.Items ?? throw new ArgumentException("schema must have items");
        if (items.Type == JsonSchemaType.Object)
        {
            throw new ArgumentException("not a primitive type");
        }

        var type = ArrayTypeMapper(schema);

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

        if (GenerateValidate)
        {
            code.AppendLine($@"
                procedure Validate(Path: Text): Text
                var
                    I: Integer;
                begin
                    for I := 0 to J.Count() - 1 do
                        if not ValidateItem(I) then
                            exit(Path + '[' + Format(I) + ']: is not type {type}: ' + GetLastErrorText());
                end;

                [TryFunction]
                local procedure ValidateItem(Index: Integer)
                var
                    T: JsonToken;
                    V: {type};
                begin
                    J.Get(Index, T);
                    V := T.AsValue().As{type}();
                end;
            ");
        }

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
            Formatter.Keywords.Contains(name))
        {
            return "Property" + name;
        }

        return Formatter.QuoteIdentifier(name);
    }
}