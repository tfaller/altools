using System.Text;
using System.Xml;

namespace TFaller.ALTools.XmlGenerator;

public class GeneratorDate(Generator generator) : IGenerator
{
    private readonly Generator _generator = generator;

    public GenerationStatus GenerateCode(StringBuilder code, XmlElement element, string siblingsPath)
    {
        var type = element.GetAttribute("type");

        if (type != "xs:date")
        {
            return GenerationStatus.Nothing;
        }

        var name = element.GetAttribute("name");
        var alName = _generator.ALName(name);

        code.AppendLine(@$"
            procedure {alName}() Date: Date
            begin
                Evaluate(Date, GetElement('{name}').InnerText(), 9);
            end;

            procedure {alName}(Value: Date)
            begin
                SetElement('{siblingsPath}', XmlElement.Create('{name}', TargetNamespace(), Format(Value, 0, 9)));
            end;
        ");

        return GenerationStatus.Getter;
    }
}