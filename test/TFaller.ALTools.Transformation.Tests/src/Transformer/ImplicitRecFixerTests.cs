using System.Linq;
using Microsoft.Dynamics.Nav.CodeAnalysis;
using TFaller.ALTools.Transformation.Transformer;

namespace TFaller.ALTools.Transformation.Tests;

public class ImplicitRecFixerTests
{
    // Objects that are available to every test case
    private static readonly string[] Dependencies =
    [
        """
        table 50000 MyTable
        {
            fields
            {
                field(1; "No."; Code[20]) { }
                field(2; Name; Text[50]) { }
            }

            procedure Calc(): Integer
            begin
            end;
        }
        """,
        """
        page 50001 MyBasePage
        {
            SourceTable = MyTable;
        }
        """
    ];

    [Theory]
    // Page: fields and procedures, explicit Rec, other record variables and own procedures stay untouched
    [InlineData(
        """
        page 50000 MyPage
        {
            SourceTable = MyTable;

            trigger OnAfterGetRecord()
            var
                Other: Record MyTable;
            begin
                Name := 'x';
                if "No." = '' then
                    Message('%1', Calc());
                Other.Name := Name;
                Rec.Name := 'y';
                Own();
            end;

            local procedure Own()
            begin
            end;
        }
        """,
        """
        page 50000 MyPage
        {
            SourceTable = MyTable;

            trigger OnAfterGetRecord()
            var
                Other: Record MyTable;
            begin
                Rec.Name := 'x';
                if Rec."No." = '' then
                    Message('%1', Rec.Calc());
                Other.Name := Rec.Name;
                Rec.Name := 'y';
                Own();
            end;

            local procedure Own()
            begin
            end;
        }
        """
    )]
    // Codeunit with TableNo
    [InlineData(
        """
        codeunit 50000 MyCodeunit
        {
            TableNo = MyTable;

            trigger OnRun()
            begin
                Name := 'x';
                Calc();
            end;
        }
        """,
        """
        codeunit 50000 MyCodeunit
        {
            TableNo = MyTable;

            trigger OnRun()
            begin
                Rec.Name := 'x';
                Rec.Calc();
            end;
        }
        """
    )]
    // Page extension: the source table is the one of the extended page
    [InlineData(
        """
        pageextension 50000 MyPageExt extends MyBasePage
        {
            trigger OnAfterGetRecord()
            begin
                Name := 'x';
                if Calc() > 0 then
                    Rec."No." := '';
            end;
        }
        """,
        """
        pageextension 50000 MyPageExt extends MyBasePage
        {
            trigger OnAfterGetRecord()
            begin
                Rec.Name := 'x';
                if Rec.Calc() > 0 then
                    Rec."No." := '';
            end;
        }
        """
    )]
    // Identifiers in a with statement and local variables with the name of a field are untouched
    [InlineData(
        """
        codeunit 50000 MyCodeunit
        {
            TableNo = MyTable;

            trigger OnRun()
            var
                Other: Record MyTable;
                Name: Text;
            begin
                Name := 'x';
                with Other do
                    Calc();
            end;
        }
        """,
        """
        codeunit 50000 MyCodeunit
        {
            TableNo = MyTable;

            trigger OnRun()
            var
                Other: Record MyTable;
                Name: Text;
            begin
                Name := 'x';
                with Other do
                    Calc();
            end;
        }
        """
    )]
    public void FixImplicitRecTest(string input, string expected)
    {
        Assert.Equal(expected, Run(input), ignoreLineEndingDifferences: true);
    }

    private static string Run(string input)
    {
        var tree = SyntaxFactory.ParseSyntaxTree(input);
        var trees = Dependencies.Select(d => SyntaxFactory.ParseSyntaxTree(d)).Append(tree).ToArray();
        var compilation = Compilation.Create("temp").AddSyntaxTrees(trees);
        var model = compilation.GetSemanticModel(tree);
        var rewriter = new ImplicitRecFixer();
        var context = rewriter.EmptyContext.WithModel(model);
        return rewriter.Rewrite(tree.GetRoot(), ref context).ToFullString();
    }
}
