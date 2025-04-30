using Microsoft.OpenApi.Models.Interfaces;
using System.Text;

namespace TFaller.ALTools.OpenApiGenerator;

public interface IGenerator
{
    /// <summary>
    /// Method generats AL code for the given schema.
    /// </summary>
    /// <param name="code">The generated code</param>
    /// <param name="name">Property/Schema name</param>
    /// <param name="schema">The schema</param>
    /// <param name="required">Whether the property is required</param>
    /// <returns>Status about what code was generated.</returns>
    public GenerationStatus GenerateCode(StringBuilder code, string name, IOpenApiSchema schema, bool required);
}