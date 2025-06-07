namespace TFaller.ALTools.XmlGenerator.Xml;

using System;
using System.Xml;
using Microsoft.Dynamics.Nav.CodeAnalysis.Utilities;

public class TransformInlineTypes
{
    public static void SequenceRefactorInlineComplexTypes(XmlElement sequence, XmlNamespaceManager mgr)
    {
        var elementsWithInlineTypes = sequence.SelectNodes("xs:element[xs:complexType]", mgr);
        if (elementsWithInlineTypes is null || elementsWithInlineTypes.Count == 0)
        {
            return;
        }

        var parentComplexTypes = (XmlElement)sequence.ParentNode!;

        var parentName = parentComplexTypes.GetAttribute("name");
        if (parentName == string.Empty)
        {
            throw new InvalidOperationException("Parent complex type must have a name attribute.");
        }

        var insertAfter = parentComplexTypes;
        var schema = (XmlElement)parentComplexTypes.ParentNode!;

        foreach (var element in elementsWithInlineTypes.Elements())
        {
            var inlineComplexType = (XmlElement)element.FirstChild!;
            var newTypeName = parentName + element.GetAttribute("name").ToPascalCase();

            element.SetAttribute("type", "tns:" + newTypeName);
            inlineComplexType.SetAttribute("name", newTypeName);

            // move inline type to the schema
            schema.InsertAfter(inlineComplexType, insertAfter);
            insertAfter = inlineComplexType;
        }
    }
}