using Microsoft.Dynamics.Nav.CodeAnalysis;

namespace TFaller.ALTools.Transformation.Tests;

public class ComplexReturnTranspilerTests
{
    [Theory]
    // Without exit
    [InlineData(
        """
        codeunit 1 A
        {
        procedure Test(): Codeunit A
        begin
        end
        }
        """,
        """
        codeunit 1 A
        {
        procedure Test(var Return: Codeunit A)
        begin
            Clear(Return);
        end
        }
        """
    )]
    // Without exit, but custom return name
    [InlineData(
        """
        codeunit 1 A
        {
        procedure Test() A: Codeunit A
        begin
        end
        }
        """,
        """
        codeunit 1 A
        {
        procedure Test(var A: Codeunit A)
        begin
            Clear(A);
        end
        }
        """
    )]
    // Empty exit
    [InlineData(
        """
        codeunit 1 A
        {
        procedure Test(): Codeunit A
        begin
            exit;
        end
        }
        """,
        """
        codeunit 1 A
        {
        procedure Test(var Return: Codeunit A)
        begin
            Clear(Return);
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
        procedure Test() A: Codeunit A
        begin
            exit(A);
        end
        }
        """,
        """
        codeunit 1 A
        {
        procedure Test(var A: Codeunit A)
        begin
            Clear(A);
            A := A;
            exit;
        end
        }
        """
    )]
    // Initialized exit
    [InlineData(
        """
        codeunit 1 A
        {
        procedure Test(): Codeunit A
        var
            A: Codeunit A;
        begin
            exit(A);
        end
        }
        """,
        """
        codeunit 1 A
        {
        procedure Test(var Return: Codeunit A)
        var
            A: Codeunit A;
        begin
            Return := A;
            exit;
        end
        }
        """
    )]
    // Named initialized exit
    [InlineData(
        """
        codeunit 1 A
        {
        procedure Test() Result: Codeunit A
        var
            A: Codeunit A;
        begin
            Result := A;
        end
        }
        """,
        """
        codeunit 1 A
        {
        procedure Test(var Result: Codeunit A)
        var
            A: Codeunit A;
        begin
            Result := A;
        end
        }
        """
    )]
    // Other parameter
    [InlineData(
        """
        codeunit 1 A
        {
        procedure Test(Param: Integer; B: Integer): Codeunit A
        begin
        end
        }
        """,
        """
        codeunit 1 A
        {
        procedure Test(Param: Integer; B: Integer; var Return: Codeunit A)
        begin
            Clear(Return);
        end
        }
        """
    )]
    // Usage
    [InlineData(
        """
        codeunit 1 A
        {
        procedure Test(): Codeunit A
        begin
        end;
        procedure Usage()
        var 
            A: Codeunit A;
        begin
            A := Test();
        end;
        }
        """,
        """
        codeunit 1 A
        {
        procedure Test(var Return: Codeunit A)
        begin
            Clear(Return);
        end;
        procedure Usage()
        var 
            A: Codeunit A;
        begin
            Test(A);
        end;
        }
        """
    )]
    // Usage with comment
    [InlineData(
        """
        codeunit 1 A
        {
        procedure Test(): Codeunit A
        begin
        end;
        procedure Usage()
        var 
            A: Codeunit A;
        begin
            // This is a comment
            A := Test();
        end;
        }
        """,
        """
        codeunit 1 A
        {
        procedure Test(var Return: Codeunit A)
        begin
            Clear(Return);
        end;
        procedure Usage()
        var 
            A: Codeunit A;
        begin
            // This is a comment
            Test(A);
        end;
        }
        """
    )]
    // Return value used without assignment
    [InlineData(
        """
        codeunit 1 A
        {
        procedure Something()
        begin
        end;
        procedure Test() A: Codeunit A
        begin
            A.Something();
        end
        }
        """,
        """
        codeunit 1 A
        {
        procedure Something()
        begin
        end;
        procedure Test(var A: Codeunit A)
        begin
            Clear(A);
            A.Something();
        end
        }
        """
    )]
    public void RewriteTest(string input, string expected)
    {
        var compilationUnit = SyntaxFactory.ParseCompilationUnit(input);
        var compilation = Compilation.Create("temp").AddSyntaxTrees(compilationUnit.SyntaxTree);
        var model = compilation.GetSemanticModel(compilationUnit.SyntaxTree);

        var rewriter = new ComplexReturnTranspiler();
        var context = rewriter.EmptyContext.WithModel(model);

        SyntaxNode result;
        do
        {
            result = rewriter.Rewrite(compilationUnit, ref context);
        }
        while (context.Dependencies.Count > 0);

        Assert.Equal(expected, result.ToFullString(), ignoreAllWhiteSpace: true, ignoreLineEndingDifferences: true);
    }
}