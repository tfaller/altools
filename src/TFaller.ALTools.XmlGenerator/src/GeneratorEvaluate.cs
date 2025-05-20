using System.Text;
using System.Xml;

namespace TFaller.ALTools.XmlGenerator;

public class GeneratorEvaluate(Generator generator) : IGenerator
{
    private readonly Generator _generator = generator;

    public GenerationStatus GenerateCode(StringBuilder code, XmlElement element, GenerationContext context)
    {
        var type = element.GetAttribute("type");
        var alType = type switch
        {
            "xs:boolean" => "Boolean",
            "xs:date" => "Date",
            "xs:dateTime" => "DateTime",
            "xs:time" => "Time",
            _ => "",
        };

        if (string.IsNullOrEmpty(alType))
        {
            return GenerationStatus.Nothing;
        }

        var name = element.GetAttribute("name");
        var alName = _generator.ALName(name);

        code.AppendLine(@$"
            procedure {alName}() Value: {alType}
            begin
                Evaluate(Value, GetElement('{name}').InnerText(), 9);
            end;

            procedure {alName}(Value: {alType})
            begin
                SetElement('{context.SiblingsPath}', XmlElement.Create('{name}', {(context.ElementFormQualified ? "TargetNamespace()" : "''")}, Format(Value, 0, 9)));
            end;
        ");

        return GenerationStatus.Getter;
    }
}