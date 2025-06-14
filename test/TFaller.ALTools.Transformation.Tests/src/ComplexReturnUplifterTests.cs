using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.Syntax;

namespace TFaller.ALTools.Transformation.Tests;

public class ComplexReturnUplifterTests
{
    [Theory]
    // Without exit
    [InlineData(
        """
        codeunit 1 A
        {
        procedure Test(var Return: Codeunit A)
        begin
        end
        }
        """,
        """
        codeunit 1 A
        {
        procedure Test() Return: Codeunit A
        begin
        end
        }
        """
    )]
    // Without exit, but custom return name
    [InlineData(
        """
        codeunit 1 A
        {
        procedure Test(var A: Codeunit A)
        begin
        end
        }
        """,
        """
        codeunit 1 A
        {
        procedure Test() A: Codeunit A
        begin
        end
        }
        """
    )]
    // Empty exit
    [InlineData(
        """
        codeunit 1 A
        {
        procedure Test(var Return: Codeunit A)
        begin
            exit;
        end
        }
        """,
        """
        codeunit 1 A
        {
        procedure Test() Return: Codeunit A
        begin
            exit;
        end
        }
        """
    )]
    // Named exit
    [InlineData(
        """
        codeunit 1 A
        {
        procedure Test(var A: Codeunit A)
        begin
            A := A;
            exit;
        end
        }
        """,
        """
        codeunit 1 A
        {
        procedure Test() A: Codeunit A
        begin
            exit(A);
        end
        }
        """
    )]

    // Usage
    [InlineData(
         """
        codeunit 1 A
        {
        procedure Test(var Return: Codeunit A)
        begin
        end;
        procedure Usage()
        var 
            A: Codeunit A;
        begin
            Test(A);
        end;
        }
        """,
        """
        codeunit 1 A
        {
        procedure Test() Return: Codeunit A
        begin
        end;
        procedure Usage()
        var 
            A: Codeunit A;
        begin
            A := Test();
        end;
        }
        """
    )]
    // Event subscriber (unchanged)
    [InlineData(
        """
        codeunit 1 A
        {
        [EventSubscriber(ObjectType::Codeunit, Codeunit::A, 'OnTest', '', false, false)]
        procedure Test(var Return: Codeunit A)
        begin
        end
        }
        """,
        """
        codeunit 1 A
        {
        [EventSubscriber(ObjectType::Codeunit, Codeunit::A, 'OnTest', '', false, false)]
        procedure Test(var Return: Codeunit A)
        begin
        end
        }
        """
    )]
    public void RewriteTest(string input, string expected)
    {
        var compilationUnit = SyntaxFactory.ParseCompilationUnit(input);
        var compilation = Compilation.Create("temp").AddSyntaxTrees(compilationUnit.SyntaxTree);
        var model = compilation.GetSemanticModel(compilationUnit.SyntaxTree);

        var rewriter = new ComplexReturnUplifter();
        var result = rewriter.Rewrite(compilationUnit, model);
        Assert.Equal(expected, result.ToFullString(), ignoreAllWhiteSpace: true, ignoreLineEndingDifferences: true);
    }
}