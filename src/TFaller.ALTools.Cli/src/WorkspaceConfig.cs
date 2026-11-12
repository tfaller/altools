using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TFaller.ALTools.Cli;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal class WorkspaceConfig
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowDuplicateProperties = false,
    };

    [JsonInclude]
    [JsonPropertyName("transformations")]
    public Dictionary<string, List<Rewriter>> Transformations { get; private set; } = [];

    [JsonInclude]
    [JsonPropertyName("projectPath")]
    public string ProjectPath { get; private set; } = string.Empty;

    public string ConfigPath { get; private set; } = string.Empty;

    public static WorkspaceConfig LoadConfig(string file)
    {
        // Make sure the file is an absolute path,
        // makes things easier later on for us.
        file = Path.GetFullPath(file);

        if (Directory.Exists(file))
        {
            throw new FileNotFoundException($"expected a config file but got a directory: '{file}'", file);
        }

        if (!File.Exists(file))
        {
            throw new FileNotFoundException($"config file not found: '{file}'", file);
        }

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
[JsonDerivedType(typeof(RewriterCommentTransformParam), "comment-transform-param")]
[JsonDerivedType(typeof(RewriterCommentTransformCond), "comment-transform-cond")]
[JsonDerivedType(typeof(RewriterCommentQuerySync), "comment-query-sync")]
[JsonDerivedType(typeof(RewriterComplexReturnTranspiler), "complex-return-transpiler")]
[JsonDerivedType(typeof(RewriterComplexReturnUplifter), "complex-return-uplifter")]
[JsonDerivedType(typeof(RewriterTempBlobUplifter), "temp-blob-uplifter")]
[JsonDerivedType(typeof(RewriterImplicitRecFixer), "implicit-rec-fixer")]
internal class Rewriter
{
}

internal class RewriterWithTags : Rewriter
{
    [JsonPropertyName("tags")]
    public string? Tags { get; set; }

    public HashSet<string> GetTags()
    {
        return Tags?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?.ToHashSet(StringComparer.InvariantCultureIgnoreCase) ?? new(StringComparer.InvariantCultureIgnoreCase);
    }
}

internal class RewriterCommentTransformVar : RewriterWithTags { }

internal class RewriterCommentTransformParam : RewriterWithTags { }

internal class RewriterCommentTransformCond : RewriterWithTags
{
    /// <summary>
    /// Flags that can be used in @altools:transform:TAG:cond:if:FLAG, each one either true or false.
    /// </summary>
    [JsonPropertyName("flags")]
    public Dictionary<string, bool> Flags { get; set; } = [];
}

internal class RewriterCommentQuerySync : RewriterWithTags { }

internal class RewriterComplexReturnTranspiler : Rewriter { }

internal class RewriterComplexReturnUplifter : Rewriter { }

internal class RewriterTempBlobUplifter : Rewriter { }

internal class RewriterImplicitRecFixer : Rewriter { }