using System.Collections.Generic;
using System.Linq;
using Microsoft.Dynamics.Nav.CodeAnalysis;
using TFaller.ALTools.Transformation.Transformer;

namespace TFaller.ALTools.Transformation.Tests;

public class TableFieldRenamerTests
{
    private static readonly TableFieldChanges[] Config =
    [
        new("MyTable", new Dictionary<string, TableFieldChange>
        {
            ["Name"] = new("Full Name"),
            ["No."] = new(No: 10),
            ["Old"] = new("New", 20),
        }),
        new("Ext Table", new Dictionary<string, TableFieldChange>
        {
            ["Ext Field"] = new("Renamed Ext", 60000),
        }),
    ];

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
                field(3; Old; Integer) { }
                field(4; Other; Integer) { }
            }
        }
        """,
        """
        table 50001 OtherTable
        {
            fields
            {
                field(1; "No."; Code[20]) { }
                field(2; Name; Text[50]) { }
            }
        }
        """
    ];

    [Theory]
    // reference: assignment
    [InlineData(
        """
        codeunit 50000 C
        {
            procedure Foo(var Rec: Record MyTable; Oth: Record OtherTable)
            var
                Name: Text;
            begin
                Rec.Name := 'x';
            end;
        }
        """,
        """
        codeunit 50000 C
        {
            procedure Foo(var Rec: Record MyTable; Oth: Record OtherTable)
            var
                Name: Text;
            begin
                Rec."Full Name" := 'x';
            end;
        }
        """)]
    // reference: unchanged field and renamed value
    [InlineData(
        """
        codeunit 50000 C
        {
            procedure Foo(var Rec: Record MyTable; Oth: Record OtherTable)
            var
                Name: Text;
            begin
                Rec."No." := Rec.Name;
            end;
        }
        """,
        """
        codeunit 50000 C
        {
            procedure Foo(var Rec: Record MyTable; Oth: Record OtherTable)
            var
                Name: Text;
            begin
                Rec."No." := Rec."Full Name";
            end;
        }
        """)]
    // reference: function argument
    [InlineData(
        """
        codeunit 50000 C
        {
            procedure Foo(var Rec: Record MyTable; Oth: Record OtherTable)
            var
                Name: Text;
            begin
                Rec.SetRange(Old, 1);
                Rec.Validate(Old, 1);
                Rec.TestField("No.");
            end;
        }
        """,
        """
        codeunit 50000 C
        {
            procedure Foo(var Rec: Record MyTable; Oth: Record OtherTable)
            var
                Name: Text;
            begin
                Rec.SetRange(New, 1);
                Rec.Validate(New, 1);
                Rec.TestField("No.");
            end;
        }
        """)]
    // reference: multiple arguments and FieldNo
    [InlineData(
        """
        codeunit 50000 C
        {
            procedure Foo(var Rec: Record MyTable; Oth: Record OtherTable)
            var
                Name: Text;
            begin
                Rec.SetCurrentKey(Name, Old);
                if Rec.FieldNo(Name) = 2 then;
            end;
        }
        """,
        """
        codeunit 50000 C
        {
            procedure Foo(var Rec: Record MyTable; Oth: Record OtherTable)
            var
                Name: Text;
            begin
                Rec.SetCurrentKey("Full Name", New);
                if Rec.FieldNo("Full Name") = 2 then;
            end;
        }
        """)]
    // reference: unconfigured field is kept
    [InlineData(
        """
        codeunit 50000 C
        {
            procedure Foo(var Rec: Record MyTable; Oth: Record OtherTable)
            var
                Name: Text;
            begin
                if Rec.Old = Rec.Other then;
            end;
        }
        """,
        """
        codeunit 50000 C
        {
            procedure Foo(var Rec: Record MyTable; Oth: Record OtherTable)
            var
                Name: Text;
            begin
                if Rec.New = Rec.Other then;
            end;
        }
        """)]
    // reference: other table and local variable are kept
    [InlineData(
        """
        codeunit 50000 C
        {
            procedure Foo(var Rec: Record MyTable; Oth: Record OtherTable)
            var
                Name: Text;
            begin
                Rec.SetFilter(Name, '<>%1', Oth.Name);
                Name := Oth.Name;
            end;
        }
        """,
        """
        codeunit 50000 C
        {
            procedure Foo(var Rec: Record MyTable; Oth: Record OtherTable)
            var
                Name: Text;
            begin
                Rec.SetFilter("Full Name", '<>%1', Oth.Name);
                Name := Oth.Name;
            end;
        }
        """)]
    // table extension: usage in own triggers
    [InlineData(
        """
        tableextension 50002 MyTableExt extends MyTable
        {
            fields
            {
                field(50000; Mine; Integer)
                {
                    trigger OnValidate()
                    begin
                        Name := 'x';
                        Rec.Old := 1;
                        Mine := 2;
                    end;
                }
            }
        }
        """,
        """
        tableextension 50002 MyTableExt extends MyTable
        {
            fields
            {
                field(50000; Mine; Integer)
                {
                    trigger OnValidate()
                    begin
                        "Full Name" := 'x';
                        Rec.New := 1;
                        Mine := 2;
                    end;
                }
            }
        }
        """)]
    // table extension: declaration of extension field and usages
    [InlineData(
        """
        table 50010 "Ext Table"
        {
            fields { field(1; "No."; Code[20]) { } }
        }

        tableextension 50003 ExtTableExt extends "Ext Table"
        {
            fields
            {
                field(50000; "Ext Field"; Integer)
                {
                    trigger OnValidate()
                    begin
                        Message('%1', "Ext Field");
                    end;
                }
            }
        }

        codeunit 50000 C
        {
            procedure Foo(var Rec: Record "Ext Table"; Ext: Record MyTable)
            begin
                Rec."Ext Field" := 1;
                Ext.Mine := Ext.Name;
            end;
        }
        """,
        """
        table 50010 "Ext Table"
        {
            fields { field(1; "No."; Code[20]) { } }
        }

        tableextension 50003 ExtTableExt extends "Ext Table"
        {
            fields
            {
                field(60000; "Renamed Ext"; Integer)
                {
                    trigger OnValidate()
                    begin
                        Message('%1', "Renamed Ext");
                    end;
                }
            }
        }

        codeunit 50000 C
        {
            procedure Foo(var Rec: Record "Ext Table"; Ext: Record MyTable)
            begin
                Rec."Renamed Ext" := 1;
                Ext.Mine := Ext."Full Name";
            end;
        }
        """)]
    // other objects: table relation and flowfields
    [InlineData(
        """
        table 50003 Child
        {
            fields
            {
                field(1; Parent; Code[20]) { TableRelation = MyTable."No."; }
                field(2; ParentName; Text[50]) { FieldClass = FlowField; CalcFormula = lookup(MyTable.Name where("No." = field(Parent))); }
                field(3; ParentCount; Integer) { FieldClass = FlowField; CalcFormula = count(MyTable where(Old = field(Parent))); }
            }
        }
        """,
        """
        table 50003 Child
        {
            fields
            {
                field(1; Parent; Code[20]) { TableRelation = MyTable."No."; }
                field(2; ParentName; Text[50]) { FieldClass = FlowField; CalcFormula = lookup(MyTable."Full Name" where("No." = field(Parent))); }
                field(3; ParentCount; Integer) { FieldClass = FlowField; CalcFormula = count(MyTable where(New = field(Parent))); }
            }
        }
        """)]
    // other objects: page
    [InlineData(
        """
        page 50004 MyPage
        {
            SourceTable = MyTable;

            layout
            {
                area(Content)
                {
                    field(Name; Rec.Name) { }
                    field(Name2; Name) { }
                    field(Old; Rec.Old) { }
                }
            }
        }
        """,
        """
        page 50004 MyPage
        {
            SourceTable = MyTable;

            layout
            {
                area(Content)
                {
                    field(Name; Rec."Full Name") { }
                    field(Name2; "Full Name") { }
                    field(Old; Rec.New) { }
                }
            }
        }
        """)]
    // other objects: report
    [InlineData(
        """
        report 50005 MyReport
        {
            dataset
            {
                dataitem(MyTable; MyTable)
                {
                    column(Name; Name) { }
                    column(No; "No.") { }
                    dataitem(Sub; MyTable)
                    {
                        DataItemLink = Old = field(Old);
                        DataItemLinkReference = MyTable;
                        column(SubName; Name) { }
                    }
                }
            }
        }
        """,
        """
        report 50005 MyReport
        {
            dataset
            {
                dataitem(MyTable; MyTable)
                {
                    column(Name; "Full Name") { }
                    column(No; "No.") { }
                    dataitem(Sub; MyTable)
                    {
                        DataItemLink = New = field(New);
                        DataItemLinkReference = MyTable;
                        column(SubName; "Full Name") { }
                    }
                }
            }
        }
        """)]
    // other objects: query
    [InlineData(
        """
        query 50006 MyQuery
        {
            elements
            {
                dataitem(MyTable; MyTable)
                {
                    column(Name; Name) { }
                    filter(Old; Old) { }
                }
            }
        }
        """,
        """
        query 50006 MyQuery
        {
            elements
            {
                dataitem(MyTable; MyTable)
                {
                    column(Name; "Full Name") { }
                    filter(Old; New) { }
                }
            }
        }
        """)]
    // filters: table relation
    [InlineData(
        """
        table 50003 Child
        {
            fields
            {
                field(1; Old; Code[20])
                {
                    TableRelation = if (Old = const('A')) MyTable.Name where (Old = field(Old), Name = const('x'), Other = const(1))
                        else MyTable where (Name = filter(<>''));
                }
            }
        }
        """,
        """
        table 50003 Child
        {
            fields
            {
                field(1; Old; Code[20])
                {
                    TableRelation = if (Old = const('A')) MyTable."Full Name" where (New = field(Old), "Full Name" = const('x'), Other = const(1))
                        else MyTable where ("Full Name" = filter(<>''));
                }
            }
        }
        """)]
    // filters: calc formula of the filtered table
    [InlineData(
        """
        table 50003 Child
        {
            fields
            {
                field(2; Cnt; Integer) { FieldClass = FlowField; CalcFormula = count(MyTable where(Old = field(Old), Name = field(Cnt))); }
                field(3; Cnt2; Integer) { FieldClass = FlowField; CalcFormula = count(OtherTable where(Name = const('x'))); }
            }
        }
        """,
        """
        table 50003 Child
        {
            fields
            {
                field(2; Cnt; Integer) { FieldClass = FlowField; CalcFormula = count(MyTable where(New = field(Old), "Full Name" = field(Cnt))); }
                field(3; Cnt2; Integer) { FieldClass = FlowField; CalcFormula = count(OtherTable where(Name = const('x'))); }
            }
        }
        """)]
    // filters: calc formula in a table extension
    [InlineData(
        """
        table 50003 Child
        {
            fields
            {
                field(1; Old; Code[20]) { }
                field(2; Cnt; Integer) { }
            }
        }

        tableextension 50006 MyTableExt extends MyTable
        {
            fields
            {
                field(50000; ChildCount; Integer) { FieldClass = FlowField; CalcFormula = count(Child where(Old = field(Old), Cnt = field(Name))); }
            }
        }
        """,
        """
        table 50003 Child
        {
            fields
            {
                field(1; Old; Code[20]) { }
                field(2; Cnt; Integer) { }
            }
        }

        tableextension 50006 MyTableExt extends MyTable
        {
            fields
            {
                field(50000; ChildCount; Integer) { FieldClass = FlowField; CalcFormula = count(Child where(Old = field(New), Cnt = field("Full Name"))); }
            }
        }
        """)]
    // filters: page links
    [InlineData(
        """
        table 50003 Child
        {
            fields
            {
                field(1; Old; Code[20]) { }
            }
        }

        page 50004 MyList
        {
            SourceTable = MyTable;
        }

        page 50005 MyCard
        {
            SourceTable = Child;

            layout
            {
                area(Content)
                {
                    part(Sub; MyList)
                    {
                        SubPageLink = Old = field(Old), Name = field(Old);
                    }
                }
            }

            actions
            {
                area(Processing)
                {
                    action(Open)
                    {
                        RunObject = page MyList;
                        RunPageLink = Name = field(Old), Old = const(1);
                    }
                }
            }
        }
        """,
        """
        table 50003 Child
        {
            fields
            {
                field(1; Old; Code[20]) { }
            }
        }

        page 50004 MyList
        {
            SourceTable = MyTable;
        }

        page 50005 MyCard
        {
            SourceTable = Child;

            layout
            {
                area(Content)
                {
                    part(Sub; MyList)
                    {
                        SubPageLink = New = field(Old), "Full Name" = field(Old);
                    }
                }
            }

            actions
            {
                area(Processing)
                {
                    action(Open)
                    {
                        RunObject = page MyList;
                        RunPageLink = "Full Name" = field(Old), New = const(1);
                    }
                }
            }
        }
        """)]
    // event subscriber: renamed field
    [InlineData(
        """
        codeunit 50000 Subs
        {
            [EventSubscriber(ObjectType::Table, Database::MyTable, 'OnAfterValidateEvent', 'Name', false, false)]
            local procedure A(var Rec: Record MyTable)
            begin
            end;
        }
        """,
        """
        codeunit 50000 Subs
        {
            [EventSubscriber(ObjectType::Table, Database::MyTable, 'OnAfterValidateEvent', 'Full Name', false, false)]
            local procedure A(var Rec: Record MyTable)
            begin
            end;
        }
        """)]
    // event subscriber: unconfigured field
    [InlineData(
        """
        codeunit 50000 Subs
        {
            [EventSubscriber(ObjectType::Table, Database::MyTable, 'OnAfterValidateEvent', 'Other', false, false)]
            local procedure A(var Rec: Record MyTable)
            begin
            end;
        }
        """,
        """
        codeunit 50000 Subs
        {
            [EventSubscriber(ObjectType::Table, Database::MyTable, 'OnAfterValidateEvent', 'Other', false, false)]
            local procedure A(var Rec: Record MyTable)
            begin
            end;
        }
        """)]
    // event subscriber: same field of other table
    [InlineData(
        """
        codeunit 50000 Subs
        {
            [EventSubscriber(ObjectType::Table, Database::OtherTable, 'OnAfterValidateEvent', 'Name', false, false)]
            local procedure A(var Rec: Record OtherTable)
            begin
            end;
        }
        """,
        """
        codeunit 50000 Subs
        {
            [EventSubscriber(ObjectType::Table, Database::OtherTable, 'OnAfterValidateEvent', 'Name', false, false)]
            local procedure A(var Rec: Record OtherTable)
            begin
            end;
        }
        """)]
    // event subscriber: without field
    [InlineData(
        """
        codeunit 50000 Subs
        {
            [EventSubscriber(ObjectType::Table, Database::MyTable, 'OnAfterInsertEvent', '', false, false)]
            local procedure A(var Rec: Record MyTable)
            begin
            end;
        }
        """,
        """
        codeunit 50000 Subs
        {
            [EventSubscriber(ObjectType::Table, Database::MyTable, 'OnAfterInsertEvent', '', false, false)]
            local procedure A(var Rec: Record MyTable)
            begin
            end;
        }
        """)]
    public void Rewrites(string input, string expected)
    {
        Assert.Equal(expected, Run(input), ignoreLineEndingDifferences: true);
    }

    // Declaration of the renamed table itself, without the table being available as dependency
    [Theory]
    // table declaration: fields
    [InlineData(
        """
        table 50002 MyTable
        {
            fields
            {
                field(1; "No."; Code[20]) { }
                field(2; Name; Text[50]) { }
                field(3; Old; Integer) { }
                field(4; Other; Integer) { }
            }
        }
        """,
        """
        table 50002 MyTable
        {
            fields
            {
                field(10; "No."; Code[20]) { }
                field(2; "Full Name"; Text[50]) { }
                field(20; New; Integer) { }
                field(4; Other; Integer) { }
            }
        }
        """)]
    // table declaration: keys and fieldgroups
    [InlineData(
        """
        table 50002 MyTable
        {
            fields
            {
                field(1; "No."; Code[20]) { }
                field(2; Name; Text[50]) { }
                field(3; Old; Integer) { }
            }

            keys
            {
                key(PK; "No.") { Clustered = true; }
                key(Sec; Name, Old) { }
            }

            fieldgroups
            {
                fieldgroup(DropDown; "No.", Name) { }
            }
        }
        """,
        """
        table 50002 MyTable
        {
            fields
            {
                field(10; "No."; Code[20]) { }
                field(2; "Full Name"; Text[50]) { }
                field(20; New; Integer) { }
            }

            keys
            {
                key(PK; "No.") { Clustered = true; }
                key(Sec; "Full Name", New) { }
            }

            fieldgroups
            {
                fieldgroup(DropDown; "No.", "Full Name") { }
            }
        }
        """)]
    public void RewritesTableDeclaration(string input, string expected)
    {
        Assert.Equal(expected, Run(input, dependencies: []), ignoreLineEndingDifferences: true);
    }

    // Names that are quoted in the config get unquoted if possible and the other way around
    [Fact]
    public void RenameToQuotedAndFromQuoted()
    {
        var config = new[]
        {
            new TableFieldChanges("\"My Table\"", new Dictionary<string, TableFieldChange>
            {
                ["\"Long Name\""] = new("Short"),
                ["Short2"] = new("Longer Name 2"),
            }),
        };

        var input = """
        table 50000 "My Table"
        {
            fields
            {
                field(1; "Long Name"; Text[10]) { }
                field(2; Short2; Text[10]) { }
            }
        }

        codeunit 50001 C
        {
            procedure P(R: Record "My Table")
            begin
                R."Long Name" := R.Short2;
            end;
        }
        """;

        var expected = """
        table 50000 "My Table"
        {
            fields
            {
                field(1; Short; Text[10]) { }
                field(2; "Longer Name 2"; Text[10]) { }
            }
        }

        codeunit 50001 C
        {
            procedure P(R: Record "My Table")
            begin
                R.Short := R."Longer Name 2";
            end;
        }
        """;

        Assert.Equal(expected, Run(input, [], config), ignoreLineEndingDifferences: true);
    }

    private static string Run(string input, string[]? dependencies = null, TableFieldChanges[]? config = null)
    {
        var tree = SyntaxFactory.ParseSyntaxTree(input);
        var trees = (dependencies ?? Dependencies).Select(d => SyntaxFactory.ParseSyntaxTree(d)).Append(tree).ToArray();
        var compilation = Compilation.Create("temp").AddSyntaxTrees(trees);
        var model = compilation.GetSemanticModel(tree);
        var rewriter = new TableFieldRenamer(config ?? Config);
        var context = rewriter.EmptyContext.WithModel(model);
        return rewriter.Rewrite(tree.GetRoot(), ref context).ToFullString();
    }
}
