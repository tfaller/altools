using System;
using System.IO;
using System.Threading.Tasks;

namespace TFaller.ALTools.Transformation.Tests;

public class GeneratedFileWriterTests
{
    [Fact]
    public async Task WriteMode_CreatesMissingFileAndReturnsTrue()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "generated.al");

        var isUpToDate = await GeneratedFileWriter.WriteOrCheck(path, "content", check: false);

        Assert.True(isUpToDate);
        Assert.Equal("content", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task WriteMode_OverwritesOutdatedFileAndReturnsTrue()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "generated.al");
        await File.WriteAllTextAsync(path, "old content");

        var isUpToDate = await GeneratedFileWriter.WriteOrCheck(path, "new content", check: false);

        Assert.True(isUpToDate);
        Assert.Equal("new content", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task CheckMode_MissingFile_ReturnsFalseWithoutCreatingIt()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "generated.al");

        var isUpToDate = await GeneratedFileWriter.WriteOrCheck(path, "content", check: true);

        Assert.False(isUpToDate);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task CheckMode_MatchingFile_ReturnsTrue()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "generated.al");
        await File.WriteAllTextAsync(path, "content");

        var isUpToDate = await GeneratedFileWriter.WriteOrCheck(path, "content", check: true);

        Assert.True(isUpToDate);
    }

    [Fact]
    public async Task CheckMode_OutdatedFile_ReturnsFalseWithoutModifyingIt()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "generated.al");
        await File.WriteAllTextAsync(path, "old content");

        var isUpToDate = await GeneratedFileWriter.WriteOrCheck(path, "new content", check: true);

        Assert.False(isUpToDate);
        Assert.Equal("old content", await File.ReadAllTextAsync(path));
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory().FullName;

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
