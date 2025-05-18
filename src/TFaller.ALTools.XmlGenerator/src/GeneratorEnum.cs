using System.Text;
using System.Xml;

namespace TFaller.ALTools.XmlGenerator;

class GeneratorEnum(Generator generator)
{
    private readonly Generator _generator = generator;

    public void GenerateCode(StringBuilder code, XmlElement element, string targetNamespace)
    {
        if (element.LocalName != "simpleType" || element.NamespaceURI != Generator.XSNamespace)
        {
            return;
        }

        if (element.FirstChild is not XmlElement restriction
            || restriction.LocalName != "restriction"
            || restriction.NamespaceURI != Generator.XSNamespace)
        {
            return;
        }

        var baseType = restriction.GetAttribute("base");
        if (baseType != "xs:string")
        {
            return;
        }

        var baseAlType = "Text";
        var name = element.GetAttribute("name");
        var alName = _generator.ALName(_generator.TypeName(targetNamespace, name));

        code.AppendLine(@$"
            Codeunit {_generator.GetFreeCodeunitId()} {alName} {{
                var _V: {baseAlType};
                var _S: Boolean;

                procedure FromValue(Value: {baseAlType})
                begin
                    Value(Value);
                end;

                procedure FromXml(Element: XmlElement)
                begin
                    Value(Element.InnerText());
                end;

                procedure AsXmlElement(): XmlElement
                begin
                    exit(XmlElement.Create('{name}', '', Value()));
                end;

                procedure Value(): {baseAlType}
                begin
                    if not _S then
                        Error('{alName}: Value not set');
                    exit(_V);
                end;

                local procedure Value(Value: {baseAlType})
                begin
                    if _S then
                        Error('{alName}: Value already set (immutable Codeunit)');
                    _S := true;
                    _V := Value;
                end;
        ");

        foreach (var e in restriction.ChildElements())
        {
            if (e.LocalName != "enumeration")
            {
                continue;
            }

            var value = e.GetAttribute("value");
            var alValueName = _generator.ALName(value);

            code.AppendLine(@$"
                procedure {alValueName}(var Value: Codeunit {alName})
                var NewObj: Codeunit {alName};
                begin
                    NewObj.FromValue('{value}');
                    Value := NewObj;
                end;");
        }

        code.AppendLine("}");
    }
}