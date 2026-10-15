using System.Linq;
using System.Threading.Tasks;
using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.Diagnostics;

namespace TFaller.ALTools.Cop.Tests;

public class NotRequiresParenthesesAnalyzerTests
{
    [Theory]
    [InlineData("not a and b", 1)]
    [InlineData("a and not b", 1)]
    [InlineData("a or not b", 1)]
    [InlineData("(not a) and b", 0)]
    [InlineData("a and (not b)", 0)]
    [InlineData("not (a and b)", 0)]
    [InlineData("not a", 0)]
    public async Task Analyze(string expression, int expectedCount)
    {
        var tree = SyntaxFactory.ParseSyntaxTree(
            $$"""
            codeunit 50000 Test
            {
                procedure Test(a: Boolean; b: Boolean): Boolean
                begin
                    exit({{expression}});
                end;
            }
            """);

        var comp = Compilation.Create("Test").AddSyntaxTrees(tree);
        var withAnalyzers = new CompilationWithAnalyzers(
            comp,
            [new NotRequiresParenthesesAnalyzer()],
            new CompilationWithAnalyzersOptions(new AnalyzerOptions([]), null!, false, false, false));

        var diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync();

        Assert.Equal(expectedCount, diagnostics.Count(d => d.Id == NotRequiresParenthesesAnalyzer.DiagnosticId));
    }
}
