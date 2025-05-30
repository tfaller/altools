using System.Text;
using System.Xml;

namespace TFaller.ALTools.XmlGenerator;

public class GeneratorString(Generator generator) : IGenerator
{
    private readonly Generator _generator = generator;

    public GenerationStatus GenerateCode(StringBuilder code, XmlElement element, GenerationContext context)
    {
        var type = element.GetAttribute("type");
        if (type.Split(':') is not [var typePrefix, var typeName])
        {
            return GenerationStatus.Nothing;
        }

        var typeNamespace = _generator.Manager.LookupNamespace(typePrefix);
        if (typeNamespace != Generator.XSNamespace || (typeName != "string" && typeName != "base64Binary"))
        {
            return GenerationStatus.Nothing;
        }

        var name = element.GetAttribute("name");
        var alName = _generator.ALName(name);

        code.AppendLine(@$"
            procedure {alName}(): Text
            begin
                exit(GetElement('{name}').InnerText());
            end;

            procedure {alName}(Value: Text)
            begin
                SetElement('{context.SiblingsPath}', XmlElement.Create('{name}', {(context.ElementFormQualified ? "TargetNamespace()" : "''")}, Value));
            end;
        ");

        return GenerationStatus.Getter;
    }
}