namespace TFaller.ALTools.Cli.Analyzer;

using System.Text.Json.Serialization;

public sealed class GitlabCodeQualityIssue
{
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("check_name")]
    public string CheckName { get; set; } = string.Empty;

    [JsonPropertyName("fingerprint")]
    public string Fingerprint { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public GitlabSeverity Severity { get; set; } = GitlabSeverity.Info;

    [JsonPropertyName("location")]
    public GitlabCodeQualityLocation Location { get; set; } = new GitlabCodeQualityLocation();
}

public sealed class GitlabCodeQualityLocation
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("lines")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GitlabCodeQualityLines? Lines { get; set; }

    [JsonPropertyName("positions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GitlabCodeQualityPositions? Positions { get; set; }
}

public sealed class GitlabCodeQualityLines
{
    [JsonPropertyName("begin")]
    public int Begin { get; set; }
}

public sealed class GitlabCodeQualityPositions
{
    [JsonPropertyName("begin")]
    public GitlabCodeQualityPositionBegin? Begin { get; set; }
}

public sealed class GitlabCodeQualityPositionBegin
{
    [JsonPropertyName("line")]
    public int Line { get; set; }

    [JsonPropertyName("column")]
    public int? Column { get; set; }
}

public enum GitlabSeverity
{
    [JsonStringEnumMemberName("info")]
    Info,

    [JsonStringEnumMemberName("minor")]
    Minor,

    [JsonStringEnumMemberName("major")]
    Major,

    [JsonStringEnumMemberName("critical")]
    Critical,

    [JsonStringEnumMemberName("blocker")]
    Blocker,
}