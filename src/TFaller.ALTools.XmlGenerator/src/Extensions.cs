using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace TFaller.ALTools.XmlGenerator;

public static class Extensions
{
    public static IEnumerable<XmlElement> ChildElements(this XmlElement element)
    {
        return element.ChildNodes.Elements();
    }

    public static IEnumerable<XmlElement> Elements(this XmlNodeList nodeList)
    {
        return nodeList.OfType<XmlElement>();
    }
}