using System.Text;
using System.Xml;

namespace TFaller.ALTools.XmlGenerator;

public class GeneratorDate(Generator generator) : IGenerator
{
    private readonly Generator _generator = generator;

    public GenerationStatus GenerateCode(StringBuilder code, XmlElement element)
    {
        var type = element.GetAttribute("type");

        if (type != "xs:date")
        {
            return GenerationStatus.Nothing;
        }

        var name = element.GetAttribute("name");

        code.AppendLine(@$"
            procedure {_generator.ALName(name)}() Date: Date
            begin
                Evaluate(Date, GetElement('{name}').InnerText(), 9);
            end;
        ");

        return GenerationStatus.Getter;
    }
}