using System.Text;
using System.Xml;

namespace TFaller.ALTools.XmlGenerator;

public class GeneratorString : IGenerator
{
    public GenerationStatus GenerateCode(StringBuilder code, XmlElement element)
    {
        var type = element.GetAttribute("type");

        if (type != "xs:string")
        {
            return GenerationStatus.Nothing;
        }

        var name = element.GetAttribute("name");

        code.AppendLine(@$"
            procedure {name}(): Text
            begin
                exit(GetElement('{name}').InnerText());
            end;
        ");

        return GenerationStatus.Getter;
    }
}