using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TFaller.ALTools.Cli;

internal class WorkspaceConfig
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    [JsonPropertyName("transformations")]
    public Dictionary<string, List<Rewriter>> Transformations
    {
        get
        {
            return field ?? [];
        }
        set;
    }

    [JsonPropertyName("projectPath")]
    public string ProjectPath { get; private set; } = string.Empty;

    public string ConfigPath { get; private set; } = string.Empty;

    public static WorkspaceConfig LoadConfig(string file)
    {
        // Make sure the file is an absolute path,
        // makes things easier later on for us.
        file = Path.GetFullPath(file);

        var json = File.ReadAllText(file)
            ?? throw new FileNotFoundException("no file found", file);

        var cfg = JsonSerializer.Deserialize<WorkspaceConfig>(json, _jsonOptions)
            ?? throw new InvalidOperationException("no config found");

        cfg.ConfigPath = file;

        if (cfg.ProjectPath == string.Empty)
        {
            cfg.ProjectPath = Path.GetDirectoryName(file) ?? throw new InvalidOperationException("no project path found");
        }

        return cfg;
    }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(RewriterCommentTransformVar), "comment-transform-var")]
[JsonDerivedType(typeof(RewriterComplexReturnTranspiler), "complex-return-transpiler")]
[JsonDerivedType(typeof(RewriterComplexReturnUplifter), "complex-return-uplifter")]
internal class Rewriter
{
}

internal class RewriterCommentTransformVar : Rewriter
{
    [JsonPropertyName("tags")]
    public string? Tags { get; set; }
}

internal class RewriterComplexReturnTranspiler : Rewriter { }

internal class RewriterComplexReturnUplifter : Rewriter { }