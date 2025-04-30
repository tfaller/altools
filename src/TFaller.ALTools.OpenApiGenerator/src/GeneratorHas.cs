using Microsoft.OpenApi.Models.Interfaces;
using System.Text;

namespace TFaller.ALTools.OpenApiGenerator;

public class GeneratorHas(Generator generator) : IGenerator
{
    private readonly Generator _generator = generator;

    public GenerationStatus GenerateCode(StringBuilder code, string name, IOpenApiSchema schema, bool required)
    {
        if (required)
        {
            return GenerationStatus.Nothing;
        }

        var alName = _generator.ALName(name);

        code.AppendLine($@"
            procedure Has{alName}(): Boolean
            var 
                Token: JsonToken;
            begin
                if not J.Get('{name}', Token) then
                    exit(false);
                if Token.IsValue() then
                    exit(not Token.AsValue().IsNull());
                exit(true);
            end;

            procedure Remove{alName}() begin
                if J.Contains('{name}') then
                    J.Remove('{name}');
            end;
        ");

        return GenerationStatus.Has;
    }
}