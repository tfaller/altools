using Microsoft.Dynamics.Nav.CodeAnalysis;

namespace TFaller.ALTools.Transformation.Tests;

public class FormatterTests
{
    [Fact]
    public void FormatTest()
    {
        var proc = SyntaxFactory.ParseSyntaxTree(
            """
            Codeunit 1 Test{
            procedure Test():Integer
            var I:Integer;begin I:=1;exit(i);end;
            }
            """);

        using var formatter = new Formatter();
        var formattedCode = formatter.Format(proc.GetRoot()).ToFullString();

        Assert.Equal(
            """
            Codeunit 1 Test
            {
                procedure Test(): Integer
                var
                    I: Integer;
                begin
                    I := 1;
                    exit(i);
                end;
            }
            """, formattedCode, false, true);
    }
}