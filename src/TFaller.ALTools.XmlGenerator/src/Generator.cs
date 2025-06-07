using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using Microsoft.Dynamics.Nav.CodeAnalysis.Utilities;
using TFaller.ALTools.Transformation;
using TFaller.ALTools.XmlGenerator.Xml;

namespace TFaller.ALTools.XmlGenerator;

public class Generator
{
    public static readonly string XSNamespace = "http://www.w3.org/2001/XMLSchema";
    public static readonly StringComparison ALStringComparison = StringComparison.InvariantCultureIgnoreCase;
    private readonly StringBuilder _code = new();
    private readonly XmlNamespaceManager _manager;
    private readonly XmlDocument _document;
    private int _nextCodeunitId = 1;
    private readonly HashSet<int> _existingCodeunitIds = [];
    private readonly Dictionary<string, Dictionary<string, string>> _typeNames = [];
    private readonly Dictionary<string, List<Replacer>> _typeNamerReplacers;
    private readonly HashSet<string> _generatedTypeNames = new(StringComparer.InvariantCultureIgnoreCase);
    private readonly List<IGenerator> _generators;
    private readonly GeneratorEnum _generatorEnum;

    public Generator(XmlDocument document, Dictionary<string, List<Replacer>>? replacers = null)
    {
        _document = document;
        _typeNamerReplacers = replacers ?? [];

        _manager = new XmlNamespaceManager(document.NameTable);
        _manager.AddNamespace("xs", XSNamespace);
        _manager.AddNamespace("wsdl", "http://schemas.xmlsoap.org/wsdl/");
        _manager.AddNamespace("soap", "http://schemas.xmlsoap.org/wsdl/soap/");

        _generators = [
            new GeneratorString(this),
            new GeneratorEvaluate(this),
            new GeneratorComplex(this),
            new GeneratorOptional(this),
        ];

        _generatorEnum = new(this);
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
            _manager.PushScope();

            foreach (var attr in element.Attributes)
            {
                if (attr is XmlAttribute attribute && attribute.Prefix == "xmlns")
                {
                    _manager.AddNamespace(attribute.LocalName, attribute.Value);
                }
            }

            foreach (var sequence in element.SelectNodes("xs:complexType/xs:sequence", _manager)!.Elements())
            {
                // make sure we don't have nested/inline complex types
                TransformInlineTypes.SequenceRefactorInlineComplexTypes(sequence, _manager);
            }

            var targetNamespace = element.GetAttribute("targetNamespace");
            var elementFormDefault = element.GetAttribute("elementFormDefault");

            foreach (var complexType in element.SelectNodes("xs:complexType", _manager)!.Elements())
            {
                GenerateComplexType(complexType, targetNamespace, elementFormDefault == "qualified");
            }

            foreach (var simpleElement in element.SelectNodes("xs:simpleType", _manager)!.Elements())
            {
                _generatorEnum.GenerateCode(_code, simpleElement, targetNamespace);
            }

            _manager.PopScope();
        }
    }

    private void GenerateComplexType(XmlElement complexType, string targetNamespace, bool elementFormDefault)
    {
        var name = complexType.GetAttribute("name");
        var alName = Formatter.QuoteIdentifier(TypeName(targetNamespace, name));

        _code.AppendLine(@$"
            Codeunit {GetFreeCodeunitId()} {alName} {{
                var _E: XmlElement;
                var _I: Boolean;

                procedure TargetNamespace(): Text
                begin
                    exit('{targetNamespace}');
                end;

                procedure FromXml(Element: XmlElement)
                begin
                    _E := Element;
                end;

                procedure AsXmlElement(): XmlElement
                begin
                    exit(_E);
                end;
        ");

        foreach (var element in complexType.ChildElements())
        {
            if (element.Name == "xs:sequence")
            {
                GenerateSequence(element, elementFormDefault);
            }

            if (element.Name == "xs:complexContent")
            {
                var extension = element.SelectSingleNode("xs:extension", _manager);
                var sequence = extension?.SelectSingleNode("xs:sequence", _manager);

                if (sequence is XmlElement sequenceElement)
                {
                    GenerateSequence(sequenceElement, elementFormDefault);
                }
            }
        }

        _code.AppendLine(@$"
            local procedure GetElement(name: Text): XmlElement
            var
                Elements: XmlNodeList;
                Node: XmlNode;
            begin
                Elements := _E.GetChildElements(name);
                if (Elements.Count <> 1) then
                    Error('Invalid XML: %1, expected 1, got %2 elements', name, Elements.Count);

                Elements.Get(1, Node);
                exit(Node.AsXmlElement());
            end;
            
            local procedure SetElement(SiblingsPath: Text; Element: XmlElement)
            var
                Nodes: XmlNodeList;
                Node: XmlNode;
            begin
                if not _I then begin
                    _E := XmlElement.Create('{name}', TargetNamespace(), Element);
                    _I := true;
                    exit;
                end;

                Nodes := _E.GetChildElements(Element.LocalName(), Element.NamespaceURI());

                case Nodes.Count() of
                    0:;
                    1: begin
                        Nodes.Get(1, Node);
                        Node.ReplaceWith(Element);
                        exit;
                    end;
                    else
                        Error('Invalid XML: %1, expected 0 or 1, got %2 elements', Element.LocalName(), Nodes.Count());
                end;

                if SiblingsPath <> '' then begin
                    _E.SelectNodes(SiblingsPath, Nodes);

                    if Nodes.Count() > 0 then begin
                        Nodes.Get(Nodes.Count(), Node);
                        Node.AddAfterSelf(Element);
                        exit;
                    end;
                end;

                _E.AddFirst(Element);
            end;"
        );

        _code.AppendLine("}");
    }

    public void GenerateSequence(XmlElement sequence, bool elementFormDefault)
    {
        var siblingsPath = new StringBuilder();

        foreach (var element in sequence.ChildElements())
        {
            if (element.Name == "xs:element")
            {
                var generated = GenerationStatus.Nothing;

                var context = new GenerationContext
                {
                    SiblingsPath = siblingsPath.ToString(),

                    ElementFormQualified = element.HasAttribute("form")
                        ? element.GetAttribute("form") == "qualified"
                        : elementFormDefault,
                };

                foreach (var generator in _generators)
                {
                    generated |= generator.GenerateCode(_code, element, context);
                }

                if (generated == GenerationStatus.Nothing)
                {
                    Console.WriteLine("Unsupported type: " + element.GetAttribute("type"));
                }

                if (siblingsPath.Length != 0)
                {
                    siblingsPath.Append('|');
                }
                siblingsPath.Append("(*[local-name()=''");
                siblingsPath.Append(element.GetAttribute("name"));
                siblingsPath.Append("''])");
            }
        }
    }

    public int GetFreeCodeunitId()
    {
        while (_existingCodeunitIds!.Contains(_nextCodeunitId))
            _nextCodeunitId++;

        _existingCodeunitIds.Add(_nextCodeunitId);
        return _nextCodeunitId++;
    }

    public string TypeName(string namespaceUri, string name)
    {
        if (!_typeNames.TryGetValue(namespaceUri, out var types))
        {
            _typeNames.Add(namespaceUri, types = []);
        }

        if (types.TryGetValue(name, out var typeName))
        {
            return typeName;
        }

        typeName = name;

        if (_typeNamerReplacers.TryGetValue(namespaceUri, out var replacers))
        {
            foreach (var replacer in replacers)
            {
                typeName = replacer.Replace(name);
                if (typeName != name)
                {
                    break;
                }
            }
        }

        if (_generatedTypeNames.Contains(typeName))
        {
            if (typeName == name)
            {
                throw new InvalidOperationException($"Type '{name}' already exists, please rename with 'typeRenamePatterns'");
            }
            throw new InvalidOperationException($"Type '{name}' was renamed in already existing '{typeName}', please rename differently with 'typeRenamePatterns'");
        }

        if (typeName.Length > 30)
        {
            throw new InvalidOperationException(
                $"Type '{typeName}' {(name != typeName ? $"(original '${name})'" : "")} is too long (max. 30 chars), please rename with 'typeRenamePatterns'");
        }

        types.Add(name, typeName);
        _generatedTypeNames.Add(typeName);
        return typeName;
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

        if (name.StartsWith('_') ||
            name.StartsWith("Get", ALStringComparison) ||
            name.StartsWith("Set", ALStringComparison) ||
            name.StartsWith("Has", ALStringComparison) ||
            name.StartsWith("Remove", ALStringComparison) ||
            name.StartsWith("Property", ALStringComparison) ||
            name.StartsWith("FromXml", ALStringComparison) ||
            name.StartsWith("AsXmlElement", ALStringComparison) ||
            name.StartsWith("TargetNamespace", ALStringComparison) ||
            name.StartsWith("Validate", ALStringComparison) ||
            Formatter.Keywords.Contains(name))
        {
            return "Property" + name;
        }

        return Formatter.QuoteIdentifier(name);
    }
}