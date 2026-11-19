using Microsoft.Dynamics.Nav.CodeAnalysis;
using TFaller.ALTools.Transformation.Transformer;

namespace TFaller.ALTools.Transformation.Tests;

public class ObjectRenamerTests
{
    private static readonly ObjectRenamer.ObjectRename[] Config =
    [
        new(ObjectRenamer.ObjectKind.Table, "Old Table", "New Table"),
        new(ObjectRenamer.ObjectKind.Codeunit, "OldCode", "New Code"),
        new(ObjectRenamer.ObjectKind.Page, "OldPage", "NewPage"),
    ];

    [Fact]
    public void RenamesDeclarationsAndReferences()
    {
        var input = """
        table 50000 "Old Table"
        {
            fields { field(1; "No."; Code[20]) { } }
        }

        codeunit 50001 OldCode
        {
            procedure Foo() begin end;
        }

        page 50002 OldPage
        {
            SourceTable = "Old Table";
            layout
            {
                area(Content)
                {
                    part(P; OldPage) { SubPageLink = "No." = field("No."); }
                }
            }
            actions
            {
                area(Processing)
                {
                    action(A) { RunObject = page OldPage; }
                    action(B) { RunObject = codeunit OldCode; }
                }
            }
        }

        codeunit 50003 C
        {
            [EventSubscriber(ObjectType::Table, Database::"Old Table", 'OnAfterInsertEvent', '', false, false)]
            local procedure Sub(var Rec: Record "Old Table") begin end;

            [EventSubscriber(ObjectType::Codeunit, Codeunit::OldCode, 'OnX', '', false, false)]
            local procedure Sub2() begin end;

            procedure P(R: Record "Old Table"; C: Codeunit OldCode; var T: Record "Old Table" temporary)
            var
                Pg: Page OldPage;
                Id: Integer;
            begin
                C.Foo();
                Page.Run(Page::OldPage);
                Codeunit.Run(Codeunit::OldCode);
                Id := Database::"Old Table";
                Pg.Run();
            end;
        }

        tableextension 50004 Ext extends "Old Table"
        {
        }

        pageextension 50005 PExt extends OldPage
        {
        }

        permissionset 50006 PS
        {
            Permissions = tabledata "Old Table" = R, codeunit OldCode = X, page OldPage = X;
        }
        """;

        var expected = """
        table 50000 "New Table"
        {
            fields { field(1; "No."; Code[20]) { } }
        }

        codeunit 50001 "New Code"
        {
            procedure Foo() begin end;
        }

        page 50002 NewPage
        {
            SourceTable = "New Table";
            layout
            {
                area(Content)
                {
                    part(P; NewPage) { SubPageLink = "No." = field("No."); }
                }
            }
            actions
            {
                area(Processing)
                {
                    action(A) { RunObject = page NewPage; }
                    action(B) { RunObject = codeunit "New Code"; }
                }
            }
        }

        codeunit 50003 C
        {
            [EventSubscriber(ObjectType::Table, Database::"New Table", 'OnAfterInsertEvent', '', false, false)]
            local procedure Sub(var Rec: Record "New Table") begin end;

            [EventSubscriber(ObjectType::Codeunit, Codeunit::"New Code", 'OnX', '', false, false)]
            local procedure Sub2() begin end;

            procedure P(R: Record "New Table"; C: Codeunit "New Code"; var T: Record "New Table" temporary)
            var
                Pg: Page NewPage;
                Id: Integer;
            begin
                C.Foo();
                Page.Run(Page::NewPage);
                Codeunit.Run(Codeunit::"New Code");
                Id := Database::"New Table";
                Pg.Run();
            end;
        }

        tableextension 50004 Ext extends "New Table"
        {
        }

        pageextension 50005 PExt extends NewPage
        {
        }

        permissionset 50006 PS
        {
            Permissions = tabledata "New Table" = R, codeunit "New Code" = X, page NewPage = X;
        }
        """;

        Assert.Equal(expected, Run(input), ignoreLineEndingDifferences: true);
    }

    // Only the configured kind is renamed, same names of other kinds stay
    [Fact]
    public void OnlyRenamesConfiguredKind()
    {
        var input = """
        table 50000 "Old Table" { fields { field(1; "No."; Code[20]) { } } }
        page 50001 "Old Table" { SourceTable = "Old Table"; }
        codeunit 50002 C
        {
            procedure P(R: Record "Old Table"; Pg: Page "Old Table")
            begin
                Page.Run(Page::"Old Table");
            end;
        }
        """;

        var expected = """
        table 50000 "New Table" { fields { field(1; "No."; Code[20]) { } } }
        page 50001 "Old Table" { SourceTable = "New Table"; }
        codeunit 50002 C
        {
            procedure P(R: Record "New Table"; Pg: Page "Old Table")
            begin
                Page.Run(Page::"Old Table");
            end;
        }
        """;

        Assert.Equal(expected, Run(input), ignoreLineEndingDifferences: true);
    }

    // Number can be changed with or without renaming
    [Fact]
    public void ChangesObjectNumber()
    {
        var input = """
        codeunit 50000 OldCode { }
        table 50001 "Old Table" { fields { field(1; "No."; Code[20]) { } } }
        page 50002 OldPage { SourceTable = "Old Table"; }
        """;

        var expected = """
        codeunit 60000 "New Code" { }
        table 60001 "Old Table" { fields { field(1; "No."; Code[20]) { } } }
        page 60002 OldPage { SourceTable = "Old Table"; }
        """;

        var config = new ObjectRenamer.ObjectRename[]
        {
            new(ObjectRenamer.ObjectKind.Codeunit, "OldCode", "New Code", 60000),
            new(ObjectRenamer.ObjectKind.Table, "Old Table", NewNo: 60001),
            new(ObjectRenamer.ObjectKind.Page, "OldPage", NewNo: 60002),
        };
        Assert.Equal(expected, Run(input, config), ignoreLineEndingDifferences: true);
    }

    // Names are matched case insensitive and quotes in the config don't matter
    [Fact]
    public void ConfigNamesAreNormalized()
    {
        var input = """
        codeunit 50000 OldCode { }
        codeunit 50001 C { var X: Codeunit oldcode; }
        """;

        var expected = """
        codeunit 50000 NewName { }
        codeunit 50001 C { var X: Codeunit NewName; }
        """;

        var config = new[] { new ObjectRenamer.ObjectRename(ObjectRenamer.ObjectKind.Codeunit, "\"OLDCODE\"", "NewName") };
        Assert.Equal(expected, Run(input, config), ignoreLineEndingDifferences: true);
    }

    private static string Run(string input, ObjectRenamer.ObjectRename[]? config = null)
    {
        var tree = SyntaxFactory.ParseSyntaxTree(input);
        var compilation = Compilation.Create("temp").AddSyntaxTrees(tree);
        var model = compilation.GetSemanticModel(tree);
        var rewriter = new ObjectRenamer(config ?? Config);
        var context = rewriter.EmptyContext.WithModel(model);
        return rewriter.Rewrite(tree.GetRoot(), ref context).ToFullString();
    }
}
