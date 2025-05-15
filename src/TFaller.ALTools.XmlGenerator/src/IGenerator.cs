using System.Text;
using System.Xml;

namespace TFaller.ALTools.XmlGenerator;

public interface IGenerator
{
    /// <summary>
    /// Method generats AL code for the given element.
    /// </summary>
    /// <param name="code">The generated code</param>
    /// <param name="element">The element</param>
    /// <returns>Status about what code was generated.</returns>
    public GenerationStatus GenerateCode(StringBuilder code, XmlElement element);
}