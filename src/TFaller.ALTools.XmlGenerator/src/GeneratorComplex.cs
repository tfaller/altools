using System.Text;
using System.Xml;
using TFaller.ALTools.Transformation;

namespace TFaller.ALTools.XmlGenerator;

public class GeneratorComplex(Generator generator) : IGenerator
{
    private readonly Generator _generator = generator;

    public GenerationStatus GenerateCode(StringBuilder code, XmlElement element, string siblingsPath)
    {
        var type = element.GetAttribute("type");
        if (type.Split(':') is not [var typePrefix, var typeName])
        {
            return GenerationStatus.Nothing;
        }

        var typeNamespace = _generator.Manager.LookupNamespace(typePrefix);
        if (typeNamespace is null || typeNamespace == Generator.XSNamespace)
        {
            return GenerationStatus.Nothing;
        }

        var name = element.GetAttribute("name");
        var alName = _generator.ALName(name);
        var alType = Formatter.QuoteIdentifier(_generator.TypeName(typeNamespace, typeName));

        code.AppendLine(@$"
            procedure Get{alName}(var Value: Codeunit {alType})
            var 
                NewObj: Codeunit {alType};
            begin
                NewObj.FromXml(GetElement('{name}'));
                Value := NewObj;
            end;

            procedure Set{alName}(Value: Codeunit {alType})
            begin
                SetElement('{siblingsPath}', Value.AsXmlElement());
            end;
        ");

        return GenerationStatus.Getter | GenerationStatus.Setter;
    }
}