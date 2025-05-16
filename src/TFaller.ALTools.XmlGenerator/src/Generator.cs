using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using Microsoft.Dynamics.Nav.CodeAnalysis.Utilities;
using TFaller.ALTools.Transformation;

namespace TFaller.ALTools.XmlGenerator;

public class Generator
{
    public static readonly StringComparison ALStringComparison = StringComparison.InvariantCultureIgnoreCase;
    private readonly StringBuilder _code = new();
    private readonly XmlNamespaceManager _manager;
    private readonly XmlDocument _document;
    private int _nextCodeunitId = 1;
    private readonly HashSet<int> _existingCodeunitIds = [];
    private readonly List<IGenerator> _generators;

    public Generator(XmlDocument document)
    {
        _document = document;

        _manager = new XmlNamespaceManager(document.NameTable);
        _manager.AddNamespace("xs", "http://www.w3.org/2001/XMLSchema");
        _manager.AddNamespace("wsdl", "http://schemas.xmlsoap.org/wsdl/");
        _manager.AddNamespace("soap", "http://schemas.xmlsoap.org/wsdl/soap/");

        _generators = [
            new GeneratorString(this),
        ];
    }

    public XmlNamespaceManager Manager => _manager;

    public string GetCode()
    {
        return _code.ToString();
    }

    public void Generate()
    {
        var wsdlTypes = _document.GetElementsByTagName("wsdl:types");
        if (wsdlTypes.Count != 1)
        {
            throw new InvalidOperationException("Invalid amount of wsdl:types elements");
        }

        foreach (var element in wsdlTypes[0]!.ChildNodes.Elements())
        {
            foreach (var complexType in element.SelectNodes("xs:complexType", _manager)!.Elements())
            {
                GenerateComplexType(complexType);
            }
        }
    }

    private void GenerateComplexType(XmlElement complexType)
    {
        var name = complexType.GetAttribute("name");

        _code.AppendLine(@$"
            Codeunit {GetFreeCodeunitId()} {name} {{
                var E: XmlElement;

                procedure FromXml(Element: XmlElement)
                begin
                    E := Element;
                end;

                procedure AsXmlElement(): XmlElement
                begin
                    exit(E);
                end;
        ");

        foreach (var element in complexType.ChildElements())
        {
            if (element.Name == "xs:sequence")
            {
                GenerateSequence(element);
            }
        }

        _code.AppendLine(@"
            local procedure GetElement(name: Text): XmlElement
            var
                Elements: XmlNodeList;
                Node: XmlNode;
            begin
                Elements := E.GetChildElements(name);
                if (Elements.Count <> 1) then
                    Error('Invalid XML: %1, expected 1, got %2 elements', name, Elements.Count);

                Elements.Get(0, Node);
                exit(Node.AsXmlElement());
            end;"
        );

        _code.AppendLine("}");
    }

    public void GenerateSequence(XmlElement sequence)
    {
        foreach (var element in sequence.ChildElements())
        {
            if (element.Name == "xs:element")
            {
                var generated = GenerationStatus.Nothing;

                foreach (var generator in _generators)
                {
                    generated |= generator.GenerateCode(_code, element);
                }

                if (generated == GenerationStatus.Nothing)
                {
                    Console.WriteLine("Unsupported type: " + element.GetAttribute("type"));
                }
            }
        }
    }

    private int GetFreeCodeunitId()
    {
        while (_existingCodeunitIds!.Contains(_nextCodeunitId))
            _nextCodeunitId++;

        _existingCodeunitIds.Add(_nextCodeunitId);
        return _nextCodeunitId++;
    }

    /// <summary>
    /// Generates a valid AL name. E.g. for procedures, variables, parameters, object names
    /// </summary>
    /// <param name="name">The name which should be converted</param>
    /// <returns>Valid AL Name</returns>
    public string ALName(string name)
    {
        name = name.ToPascalCase(false)
            .Replace(" ", "")
            .Replace("&", "")
            .Replace(".", "_")
            .Replace(",", "_");

        if (name.StartsWith("Get", ALStringComparison) ||
            name.StartsWith("Set", ALStringComparison) ||
            name.StartsWith("Has", ALStringComparison) ||
            name.StartsWith("Remove", ALStringComparison) ||
            name.StartsWith("Property", ALStringComparison) ||
            name.StartsWith("FromXml", ALStringComparison) ||
            name.StartsWith("AsXmlElement", ALStringComparison) ||
            name.StartsWith("Validate", ALStringComparison) ||
            Formatter.Keywords.Contains(name))
        {
            return "Property" + name;
        }

        return Formatter.QuoteIdentifier(name);
    }
}