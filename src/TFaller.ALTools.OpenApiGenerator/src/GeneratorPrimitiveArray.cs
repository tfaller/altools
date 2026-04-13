using System.Text;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Models.Interfaces;

namespace TFaller.ALTools.OpenApiGenerator;

public class GeneratorPrimitiveArray(Generator generator) : IGenerator
{
    public GenerationStatus GenerateCode(StringBuilder code, string name, IOpenApiSchema schema, bool required)
    {
        if (schema.Type != JsonSchemaType.Array)
            return GenerationStatus.Nothing;

        var itemType = schema.Items?.Type;
        if (!GeneratorPrimitive.SupportedTypes.Contains(itemType))
            return GenerationStatus.Nothing;

        var alName = generator.ALName(name);
        var alItemType = GeneratorPrimitive.GetALTypeDefintionBySchema(schema.Items!);

        code.Append(
            $@"                                                                                                                                                                                                                   
          procedure Get{alName}List(var Result: List of [{alItemType}])                                                                                                                                                                     
          var Token: JsonToken; ArrToken: JsonToken;
          begin                                                                                                                                                                                                                         
              J.Get('{name}', Token);
              foreach ArrToken in Token.AsArray() do                                                                                                                                                                                    
                  Result.Add(ArrToken.AsValue().As{alItemType}());                                                                                                                                                                      
          end;
      ");
        
        code.Append($@"
          procedure Set{alName}List(var {alName}: List of [{alItemType}])                                                                                                                                                                   
          var Arr: JsonArray; Item: {alItemType};
          begin                                                                                                                                                                                                                         
              foreach Item in {alName} do Arr.Add(Item);
              if J.Contains('{name}') then                                                                                                                                                                                              
                  J.Replace('{name}', Arr.AsToken())                                                                                                                                                                                    
              else
                  J.Add('{name}', Arr.AsToken());                                                                                                                                                                                       
          end;    
      ");

        var status = GenerationStatus.Getter | GenerationStatus.Setter;

        if (generator.GenerateValidate)
        {
            CreateValidateCode(code, name, alName, schema, required);
            status |= GenerationStatus.Validate;
        }

        return status;
    }

    private static void CreateValidateCode(StringBuilder code, string name, string alName, IOpenApiSchema schema, bool required)
    {
        code.AppendLine($@"
            procedure Validate{alName}(Path: Text): Text
            var Token: JsonToken;
            begin
        ");

        code.AppendLine($@"if not J.Contains('{name}') then");

        if (required)
            code.AppendLine($@"exit(Path + '.{name} is required');");
        else
            code.AppendLine($@"exit('');");

        code.AppendLine($@"J.Get('{name}', Token);");

        if (schema.MaxItems != null)
            code.AppendLine($@"if Token.AsArray().Count() > {schema.MaxItems} then
                exit(Path + '.{name} count value is greater than max items');");

        if (schema.MinItems != null)
            code.AppendLine($@"if Token.AsArray().Count() < {schema.MinItems} then
                exit(Path + '.{name} count value is less than min items');");

        code.AppendLine("exit('');");
        code.AppendLine("end;");
    }
}