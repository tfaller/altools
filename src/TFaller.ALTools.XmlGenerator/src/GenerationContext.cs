namespace TFaller.ALTools.XmlGenerator;

public readonly ref struct GenerationContext
{
    public string SiblingsPath { get; init; }
    public bool ElementFormQualified { get; init; }
}