using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TFaller.ALTools.OpenApiGenerator;

public class Config
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    [JsonPropertyName("definitions")]
    public List<Definition>? Definitions { get; set; }

    [JsonPropertyName("projectPath")]
    public string ProjectPath { get; private set; } = string.Empty;

    public string ConfigPath { get; private set; } = string.Empty;

    public static Config LoadConfig(string file)
    {
        // Make sure the file is an absolute path,
        // makes things easier later on for us.
        file = Path.GetFullPath(file);

        var json = File.ReadAllText(file)
            ?? throw new FileNotFoundException("no file found", file);

        var cfg = JsonSerializer.Deserialize<Config>(json, _jsonOptions)
            ?? throw new InvalidOperationException("no config found");

        cfg.ConfigPath = file;

        if (cfg.ProjectPath == string.Empty)
        {
            cfg.ProjectPath = Path.GetDirectoryName(file) ?? throw new InvalidOperationException("no project path found");
        }

        return cfg;
    }
}

public class Definition
{
    [JsonPropertyName("schemaFile")]
    public string? SchemaFile { get; set; }

    [JsonPropertyName("mergedCodeunitName")]
    public string? MergedCodeunitName { get; set; }

    [JsonPropertyName("mergedCodeunitId")]
    public int? MergedCodeunitId { get; set; }

    [JsonPropertyName("mergedCodeunitFile")]
    public string? MergedCodeunitFile { get; set; }

    [JsonPropertyName("generateValidate")]
    public bool GenerateValidate { get; set; } = false;
}