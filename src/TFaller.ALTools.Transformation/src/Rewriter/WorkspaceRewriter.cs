using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
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

        comp = WorkspaceHelper.LoadReferences(comp, workspace + "/.alpackages");
        comp = await WorkspaceHelper.LoadFilesAsync(comp, workspace, parseOptions, files);

        var originalFiles = files.ToImmutableDictionary();

        // we cant run rewriters in parallel, because they might break each other
        foreach (var rewriter in rewriters)
        {
            var rewriterComp = comp;
            var emptyContext = rewriter.EmptyContext;
            var batch = new Dictionary<string, SyntaxTree>(files);
            var dependencies = new Dictionary<string, HashSet<string>>();
            var contexts = new ConcurrentDictionary<SyntaxTree, IRewriterContext>();

            while (batch.Count > 0)
            {
                // however, we can run a rewriter in parrallel over all files
                await Parallel.ForEachAsync(batch, async (kvp, token) =>
                {
                    var fileName = kvp.Key;
                    var syntaxTree = kvp.Value;
                    var originalSyntaxTree = originalFiles[fileName];

                    if (!contexts.TryGetValue(originalSyntaxTree, out var context))
                    {
                        // we did not yet run the rewriter on this file, so we need to create a new context
                        context = emptyContext.WithModel(rewriterComp.GetSemanticModel(syntaxTree));
                    }

                    // make latest contexts available to the rewriter
                    context = context.WithContexts(contexts.ToImmutableDictionary());

                    // actually rewrite the syntax tree
                    var newSyntaxTree = rewriter.Rewrite(await syntaxTree.GetRootAsync(token), ref context).SyntaxTree;

                    // save context, maybe we visit the file again, or other rewriters need it
                    contexts[originalSyntaxTree] = context;

                    lock (dependencies)
                    {
                        foreach (var dep in context.Dependencies)
                        {
                            var depName = dep.FilePath;
                            if (depName != string.Empty)
                            {
                                if (!dependencies.TryGetValue(depName, out var depSet))
                                {
                                    dependencies[depName] = depSet = [];
                                }
                                depSet.Add(fileName);
                            }
                        }
                    }

                    if (syntaxTree == newSyntaxTree)
                        // no change, no need to update
                        return;

                    lock (files)
                    {
                        // we need to lock here, because multiple threads might write to the same dictionary
                        // this is not a problem, because we only update the syntax tree, not the file name
                        files[fileName] = newSyntaxTree;
                        filesChanged.Add(fileName);
                    }
                });

                batch.Clear();
                foreach (var deps in dependencies)
                {
                    // if the file was changed, we need to reprocess all dependants
                    foreach (var dep in deps.Value)
                    {
                        batch[dep] = files[dep];
                    }
                }
                dependencies.Clear();
            }

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