using System.Text;
using System.Xml;
using TFaller.ALTools.Transformation;

namespace TFaller.ALTools.XmlGenerator;

public class GeneratorArray(Generator generator) : IGenerator
{
    private readonly Generator _generator = generator;

    public GenerationStatus GenerateCode(StringBuilder code, XmlElement element, GenerationContext context)
    {
        if (element.GetAttribute("maxOccurs") != "unbounded")
        {
            return GenerationStatus.Nothing;
        }

        var name = element.GetAttribute("name");
        var alName = _generator.ALName(name);
        var type = element.GetAttribute("type");

        if (type.Split(':') is [var typePrefix, var typeName])
        {
            var typeNamespace = _generator.Manager.LookupNamespace(typePrefix);
            if (typeNamespace != null && typeNamespace != Generator.XSNamespace)
            {
                var alType = Formatter.QuoteIdentifier(_generator.TypeName(typeNamespace, typeName));
                GenerateComplexArrayMethods(code, name, alName, alType, context);
                return GenerationStatus.Array;
            }
        }

        return GenerationStatus.Nothing;
    }

    private static void GenerateComplexArrayMethods(StringBuilder code, string name, string alName, string alType, GenerationContext context)
    {
        code.AppendLine(@$"
            procedure {Formatter.CombineIdentifiers("Add", alName)}(Item: Codeunit {alType})
            var
                Nodes: XmlNodeList;
                Node: XmlNode;
            begin
                if not _I then begin
                    Set{alName}(Item);
                    exit;
                end;

                Nodes := _E.GetChildElements('{name}');
                if Nodes.Count() = 0 then begin
                    Set{alName}(Item);
                    exit;
                end;

                Nodes.Get(Nodes.Count(), Node);
                Node.AddAfterSelf(Item.AsXmlElementWithName('{name}', {(context.ElementFormQualified ? "TargetNamespace()" : "''")}));
            end;

            procedure {Formatter.CombineIdentifiers("Get", alName)}(Index: Integer; var Item: Codeunit {alType})
            var
                Nodes: XmlNodeList;
                Node: XmlNode;
                NewItem: Codeunit {alType};
            begin
                Nodes := _E.GetChildElements('{name}');
                
                if (Index < 0) or (Index >= Nodes.Count()) then
                    Error('Index out of bounds: %1 (Count: %2)', Index, Nodes.Count());
                
                Nodes.Get(Index, Node);
                NewItem.FromXml(Node.AsXmlElement());
                Item := NewItem;
            end;

            procedure {Formatter.CombineIdentifiers("Count", alName)}(): Integer
            var
                Nodes: XmlNodeList;
            begin
                Nodes := _E.GetChildElements('{name}');
                exit(Nodes.Count());
            end;

            procedure {Formatter.CombineIdentifiers("RemoveAt", alName)}(Index: Integer)
            var
                Nodes: XmlNodeList;
                Node: XmlNode;
            begin
                Nodes := _E.GetChildElements('{name}');
                
                if (Index < 0) or (Index >= Nodes.Count()) then
                    Error('Index out of bounds: %1 (Count: %2)', Index, Nodes.Count());
                
                Nodes.Get(Index, Node);
                Node.Remove();
            end;

            procedure {Formatter.CombineIdentifiers("Set", alName)}(Index: Integer; Item: Codeunit {alType})
            var
                Nodes: XmlNodeList;
                Node: XmlNode;
                NewElement: XmlElement;
            begin
                Nodes := _E.GetChildElements('{name}');
                
                if (Index < 0) or (Index >= Nodes.Count()) then
                    Error('Index out of bounds: %1 (Count: %2)', Index, Nodes.Count());
                
                NewElement := Item.AsXmlElementWithName('{name}', {(context.ElementFormQualified ? "TargetNamespace()" : "''")});
                Nodes.Get(Index, Node);
                Node.ReplaceWith(NewElement);
            end;
        ");
    }
}
