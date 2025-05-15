namespace TFaller.ALTools.XmlGenerator;

/// <summary>
/// Status about what code was generated
/// </summary>
public enum GenerationStatus : int
{
    /// <summary>
    /// No code generation was performed
    /// </summary>
    Nothing = 0,

    /// <summary>
    /// A getter procedure was generated
    /// </summary>
    Getter = 1,

    /// <summary>
    /// A setter procedure was generated
    /// </summary>
    Setter = 2,

    /// <summary>
    /// A has procedure was generated
    /// </summary>
    Has = 4,

    /// <summary>
    /// A validate procedure was generated
    /// </summary>
    Validate = 8,
}