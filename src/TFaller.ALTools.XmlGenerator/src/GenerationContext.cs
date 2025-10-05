using System.Text;

namespace TFaller.ALTools.XmlGenerator;

public readonly ref struct GenerationContext
{
    public StringBuilder SiblingsPath { get; init; }
    public bool ElementFormQualified { get; init; }
}