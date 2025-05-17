using System.Text;
using System.Xml;
using TFaller.ALTools.Transformation;

namespace TFaller.ALTools.XmlGenerator;

public class GeneratorOptional(Generator generator) : IGenerator
{
    private readonly Generator _generator = generator;

    public GenerationStatus GenerateCode(StringBuilder code, XmlElement element, string siblingsPath)
    {
        var minOccurs = element.GetAttribute("minOccurs");
        if (minOccurs != "0")
        {
            return GenerationStatus.Nothing;
        }

        var name = element.GetAttribute("name");
        var alName = _generator.ALName(name);

        code.AppendLine(@$"
            procedure {Formatter.CombineIdentifiers("Has", alName)}(): Boolean
            var
                Nodes: XmlNodeList;
            begin
                Nodes := E.GetChildElements('{name}');
                exit(Nodes.Count > 0);
            end;

            procedure {Formatter.CombineIdentifiers("Remove", alName)}()
            var 
                Node: XmlNode;
            begin
                foreach Node in E.GetChildElements('{name}') do
                    Node.Remove();
            end;
        ");

        return GenerationStatus.Has;
    }
}