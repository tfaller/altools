using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.Syntax;

namespace TFaller.ALTools.Transformation.Rewriter;

/// <summary>
/// Rewrites all files in a workspace using the provided rewriters.
/// </summary>
/// <param name="rewriters">Rewrites that get executed in the given order</param>
/// <param name="parseOptions">Options of the AL file parser</param>
public class WorkspaceRewriter(List<IConcurrentRewriter> rewriters, ParseOptions parseOptions)
{
    private readonly Formatter _formatter = new();

    public async Task Rewrite(string workspace)
    {
        var comp = Compilation.Create("tmp");
        var files = new Dictionary<string, SyntaxTree>();
        var filesChanged = new HashSet<string>();

        comp = await WorkspaceHelper.LoadFilesAsync(comp, workspace, parseOptions, files);

        // we cant run rewriters in parallel, because they might break each other
        foreach (var rewriter in rewriters)
        {
            var rewriterComp = comp;
            var emptyContext = rewriter.EmptyContext;

            // however, we can run a rewriter in parrallel over all files
            await Parallel.ForEachAsync(files, async (kvp, token) =>
            {
                var syntaxTree = kvp.Value;
                var model = rewriterComp.GetSemanticModel(syntaxTree);
                var context = emptyContext.WithModel(model);
                var newSyntaxTree = rewriter.Rewrite(await syntaxTree.GetRootAsync(token), ref context).SyntaxTree;

                if (syntaxTree == newSyntaxTree)
                    // no change, no need to update
                    return;

                lock (files)
                {
                    // we need to lock here, because multiple threads might write to the same dictionary
                    // this is not a problem, because we only update the syntax tree, not the file name
                    files[kvp.Key] = newSyntaxTree;
                    filesChanged.Add(kvp.Key);
                }
            });

            comp = comp
                .RemoveAllSyntaxTrees()
                .AddSyntaxTrees(files.Values);
        }

        var formattedFiles = new Dictionary<string, string>();
        foreach (var file in filesChanged)
        {
            // format the changed files
            // as the formatter comments, we do it one after another, because it is not faster to run it in parallel
            var formatted = _formatter.Format(files[file].GetRoot());

            // toString seems to be also not really faster if parallelized, just do it here as well sequentially
            formattedFiles[file] = formatted.SyntaxTree.ToString();
        }

        await Task.WhenAll(formattedFiles.Select(kvp =>
            File.WriteAllTextAsync(kvp.Key, kvp.Value, Encoding.UTF8)
        ));
    }
}