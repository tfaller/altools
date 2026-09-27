using System.IO;
using System.Threading.Tasks;
using TFaller.ALTools.Transformation.Rewriter;

namespace TFaller.ALTools.Transformation;

public static class GeneratedFileWriter
{
    /// <summary>
    /// Writes the generated content to disk, or, in check mode, verifies that the file on disk
    /// already matches it without writing. Useful for CI pipelines that want to fail when generated
    /// output would otherwise change.
    /// </summary>
    /// <returns>true if the file is up to date (or was written); false if it was outdated (check mode only)</returns>
    public static async Task<bool> WriteOrCheck(string path, string content, bool check)
    {
        if (!check)
        {
            await File.WriteAllTextAsync(path, content, WorkspaceRewriter.Encoding);
            return true;
        }

        var existing = File.Exists(path) ? await File.ReadAllTextAsync(path) : null;
        return existing == content;
    }
}
