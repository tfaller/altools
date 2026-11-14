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
        table 50001 OtherTable
        {
            fields
            {
                field(1; "No."; Code[20]) { }
                field(2; Name; Text[50]) { }
                field(3; Extra; Text[50]) { }
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
    // API page: ODataKeyFields must stay a plain field name
    [InlineData(
        """
        page 50000 MyApiPage
        {
            PageType = API;
            SourceTable = MyTable;
            ODataKeyFields = Name;

            trigger OnAfterGetRecord()
            begin
                Name := 'x';
            end;
        }
        """,
        """
        page 50000 MyApiPage
        {
            PageType = API;
            SourceTable = MyTable;
            ODataKeyFields = Name;

            trigger OnAfterGetRecord()
            begin
                Rec.Name := 'x';
            end;
        }
        """
    )]
    // SourceTableView: field names in where conditions must stay plain
    [InlineData(
        """
        page 50000 MyPage
        {
            SourceTable = MyTable;
            SourceTableView = sorting("No.") where(Name = const('x'), "No." = filter(<>''));

            trigger OnAfterGetRecord()
            begin
                Name := 'x';
            end;
        }
        """,
        """
        page 50000 MyPage
        {
            SourceTable = MyTable;
            SourceTableView = sorting("No.") where(Name = const('x'), "No." = filter(<>''));

            trigger OnAfterGetRecord()
            begin
                Rec.Name := 'x';
            end;
        }
        """
    )]
    // TableRelation on a page field: plain field names, no Rec prefix
    [InlineData(
        """
        page 50000 MyPage
        {
            SourceTable = MyTable;

            layout
            {
                area(Content)
                {
                    field(Other; Name)
                    {
                        TableRelation = OtherTable."No." where(Name = field(Name));
                    }
                }
            }

            trigger OnAfterGetRecord()
            begin
                Name := 'x';
            end;
        }
        """,
        """
        page 50000 MyPage
        {
            SourceTable = MyTable;

            layout
            {
                area(Content)
                {
                    field(Other; Rec.Name)
                    {
                        TableRelation = OtherTable."No." where(Name = field(Name));
                    }
                }
            }

            trigger OnAfterGetRecord()
            begin
                Rec.Name := 'x';
            end;
        }
        """
    )]
    // Other properties (Visible, ...) may contain expressions: prefixed
    [InlineData(
        """
        page 50000 MyPage
        {
            SourceTable = MyTable;

            layout
            {
                area(Content)
                {
                    field(Other; Rec.Name)
                    {
                        Visible = Name <> '';
                    }
                }
            }
        }
        """,
        """
        page 50000 MyPage
        {
            SourceTable = MyTable;

            layout
            {
                area(Content)
                {
                    field(Other; Rec.Name)
                    {
                        Visible = Rec.Name <> '';
                    }
                }
            }
        }
        """
    )]
    // Already explicit page without triggers: unchanged
    [InlineData(
        """
        page 50153 MyListPart
        {
            PageType = ListPart;
            SourceTable = OtherTable;
            CardPageId = MyBasePage;
            Editable = false;

            layout
            {
                area(Content)
                {
                    repeater(Messages)
                    {
                        field(Name; Rec.Name)
                        {
                        }
                        field("No."; Rec."No.")
                        {
                            Editable = false;
                        }
                        field(Extra; Rec.Extra) { }
                    }
                }
            }
        }
        """,
        """
        page 50153 MyListPart
        {
            PageType = ListPart;
            SourceTable = OtherTable;
            CardPageId = MyBasePage;
            Editable = false;

            layout
            {
                area(Content)
                {
                    repeater(Messages)
                    {
                        field(Name; Rec.Name)
                        {
                        }
                        field("No."; Rec."No.")
                        {
                            Editable = false;
                        }
                        field(Extra; Rec.Extra) { }
                    }
                }
            }
        }
        """
    )]
    // SubPageLink: plain field names
    [InlineData(
        """
        page 50000 MyPage
        {
            SourceTable = MyTable;

            layout
            {
                area(Content)
                {
                    part(Lines; MyBasePage)
                    {
                        SubPageLink = "No." = field("No."), Name = const('x');
                        SubPageView = where(Name = filter(<>''));
                    }
                }
            }

            trigger OnAfterGetRecord()
            begin
                Name := 'x';
            end;
        }
        """,
        """
        page 50000 MyPage
        {
            SourceTable = MyTable;

            layout
            {
                area(Content)
                {
                    part(Lines; MyBasePage)
                    {
                        SubPageLink = "No." = field("No."), Name = const('x');
                        SubPageView = where(Name = filter(<>''));
                    }
                }
            }

            trigger OnAfterGetRecord()
            begin
                Rec.Name := 'x';
            end;
        }
        """
    )]
    // TableRelation: plain field names
    [InlineData(
        """
        page 50000 MyPage
        {
            SourceTable = MyTable;

            layout
            {
                area(Content)
                {
                    field(Test; Dummy)
                    {
                        TableRelation = MyTable."No." where(Name = field(Name));
                    }
                }
            }

            trigger OnAfterGetRecord()
            begin
                Name := 'x';
            end;
        }
        """,
        """
        page 50000 MyPage
        {
            SourceTable = MyTable;

            layout
            {
                area(Content)
                {
                    field(Test; Dummy)
                    {
                        TableRelation = MyTable."No." where(Name = field(Name));
                    }
                }
            }

            trigger OnAfterGetRecord()
            begin
                Rec.Name := 'x';
            end;
        }
        """
    )]
    // RunPageLink / RunPageView: plain field names
    [InlineData(
        """
        page 50000 MyPage
        {
            SourceTable = MyTable;

            actions
            {
                area(Processing)
                {
                    action(Open)
                    {
                        RunObject = page MyBasePage;
                        RunPageLink = "No." = field("No."), Name = const('x');
                        RunPageView = where(Name = filter(<>''));
                    }
                }
            }

            trigger OnAfterGetRecord()
            begin
                Name := 'x';
            end;
        }
        """,
        """
        page 50000 MyPage
        {
            SourceTable = MyTable;

            actions
            {
                area(Processing)
                {
                    action(Open)
                    {
                        RunObject = page MyBasePage;
                        RunPageLink = "No." = field("No."), Name = const('x');
                        RunPageView = where(Name = filter(<>''));
                    }
                }
            }

            trigger OnAfterGetRecord()
            begin
                Rec.Name := 'x';
            end;
        }
        """
    )]
    // Field source expressions are rewritten
    [InlineData(
        """
        page 50000 MyPage
        {
            SourceTable = MyTable;

            layout
            {
                area(Content)
                {
                    field(Test; Name)
                    {
                    }
                    field(Test2; "No.")
                    {
                    }
                }
            }

            trigger OnAfterGetRecord()
            begin
                Name := 'x';
            end;
        }
        """,
        """
        page 50000 MyPage
        {
            SourceTable = MyTable;

            layout
            {
                area(Content)
                {
                    field(Test; Rec.Name)
                    {
                    }
                    field(Test2; Rec."No.")
                    {
                    }
                }
            }

            trigger OnAfterGetRecord()
            begin
                Rec.Name := 'x';
            end;
        }
        """
    )]
    // Field arguments of record methods stay plain, value arguments are rewritten
    [InlineData(
        """
        codeunit 50000 MyCodeunit
        {
            TableNo = MyTable;

            trigger OnRun()
            var
                Other: Record MyTable;
            begin
                Other.TestField(Name);
                Other.SetRange(Name, Name);
                Other.SetFilter("No.", '<>%1', Name);
                Other.SetCurrentKey(Name, "No.", Name);
                Other.CalcFields(Name, Name, Name);
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
            begin
                Other.TestField(Name);
                Other.SetRange(Name, Rec.Name);
                Other.SetFilter("No.", '<>%1', Rec.Name);
                Other.SetCurrentKey(Name, "No.", Name);
                Other.CalcFields(Name, Name, Name);
            end;
        }
        """
    )]
    // Fields of another table than the source table are never implicit Rec members
    [InlineData(
        """
        codeunit 50000 MyCodeunit
        {
            TableNo = MyTable;

            trigger OnRun()
            var
                Cust: Record OtherTable;
            begin
                Cust.TestField(Extra);
                Cust.SetRange(Name, Extra);
                Cust.Name := Name;
                Message('%1', Cust.Calc());
            end;
        }
        """,
        """
        codeunit 50000 MyCodeunit
        {
            TableNo = MyTable;

            trigger OnRun()
            var
                Cust: Record OtherTable;
            begin
                Cust.TestField(Extra);
                Cust.SetRange(Name, Extra);
                Cust.Name := Rec.Name;
                Message('%1', Cust.Calc());
            end;
        }
        """
    )]
    // "with" on a record of the same table as Rec: members belong to the variable, not to Rec
    [InlineData(
        """
        page 50000 MyPage
        {
            SourceTable = MyTable;

            var
                GlobalRec: Record MyTable;

            trigger OnAfterGetRecord()
            var
                Other: Record MyTable;
            begin
                with Other do begin
                    Name := Name;
                    TestField(Name);
                    Calc();
                end;
                with GlobalRec do
                    Name := 'x';
                Name := 'y';
            end;
        }
        """,
        """
        page 50000 MyPage
        {
            SourceTable = MyTable;

            var
                GlobalRec: Record MyTable;

            trigger OnAfterGetRecord()
            var
                Other: Record MyTable;
            begin
                with Other do begin
                    Name := Name;
                    TestField(Name);
                    Calc();
                end;
                with GlobalRec do
                    Name := 'x';
                Rec.Name := 'y';
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
