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

    [Theory]
    [InlineData("a", true)]
    [InlineData("_", true)]
    [InlineData("1", false)]
    [InlineData(".", false)]
    public void IdentifierRegexTest(string identifier, bool expected)
    {
        Assert.Equal(expected, Formatter.IdentifierRegex().IsMatch(identifier));
    }

    [Fact]
    public void IdentifierRegexLongTest()
    {
        // limit is acutally at 120, this regexp is just here for the content
        Assert.Matches(Formatter.IdentifierRegex(), new string('a', 121));
    }

    [Theory]
    [InlineData("Hello", "Hello")]
    [InlineData("HelloWorld", "hello", "world")]
    [InlineData("\"Hello!\"", "hello", "\"!\"")]
    public void CombineIdentifiersTest(string expected, params string[] identifiers)
    {
        Assert.Equal(expected, Formatter.CombineIdentifiers(identifiers));
    }

    [Theory]
    [InlineData("Hello", "Hello")]
    [InlineData("\"Hello!\"", "Hello!")]
    public void QuoteIdentifierTest(string expected, string identifier)
    {
        Assert.Equal(expected, Formatter.QuoteIdentifier(identifier));
    }

    [Theory]
    [InlineData("Hello", "Hello")]
    [InlineData("Hello!", "\"Hello!\"")]
    public void UnquoteIdentifierTest(string expected, string identifier)
    {
        Assert.Equal(expected, Formatter.UnquoteIdentifier(identifier));
    }
}