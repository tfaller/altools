using Microsoft.Dynamics.Nav.CodeAnalysis;
using System;
using System.Collections.Generic;
using TFaller.ALTools.Transformation.Transformer.CommentRule;

namespace TFaller.ALTools.Transformation.Tests;

public class TransformParamTests
{
    [Theory]
    // Rename + type change, body references renamed
    [InlineData(
        """
        codeunit 1 Test
        {
            // @altools:transform:cloud:param:SalesLine:NewSalesLine: Record "New Sales Line"
            procedure Do(SalesLine: Record "Sales Line"; Qty: Integer)
            begin
                SalesLine.Validate(Quantity, Qty);
            end;
        }
        """,
        """
        codeunit 1 Test
        {
            procedure Do(NewSalesLine: Record "New Sales Line"; Qty: Integer)
            begin
                NewSalesLine.Validate(Quantity, Qty);
            end;
        }
        """,
        "cloud"
    )]
    // New name omitted -> same name, only type changes
    [InlineData(
        """
        codeunit 1 Test
        {
            // @altools:transform:cloud:param:Qty: Decimal
            procedure Do(Qty: Integer)
            begin
                Qty := 1;
            end;
        }
        """,
        """
        codeunit 1 Test
        {
            procedure Do(Qty: Decimal)
            begin
                Qty := 1;
            end;
        }
        """,
        "cloud"
    )]
    // Multiple transformations for one procedure
    [InlineData(
        """
        codeunit 1 Test
        {
            // @altools:transform:cloud:param:A:B: Text[100]
            // @altools:transform:cloud:param:C: Decimal
            procedure Do(A: Text[50]; C: Integer)
            begin
                A := Format(C);
            end;
        }
        """,
        """
        codeunit 1 Test
        {
            procedure Do(B: Text[100]; C: Decimal)
            begin
                B := Format(C);
            end;
        }
        """,
        "cloud"
    )]
    // var param: var kept when new name omitted
    [InlineData(
        """
        codeunit 1 Test
        {
            // @altools:transform:cloud:param:Rec: Record "New Table"
            procedure Do(var Rec: Record "Old Table")
            begin
                Rec.Modify();
            end;
        }
        """,
        """
        codeunit 1 Test
        {
            procedure Do(var Rec: Record "New Table")
            begin
                Rec.Modify();
            end;
        }
        """,
        "cloud"
    )]
    // var param: var removed by new name without var
    [InlineData(
        """
        codeunit 1 Test
        {
            // @altools:transform:cloud:param:Rec:NewRec: Record "New Table"
            procedure Do(var Rec: Record "Old Table")
            begin
                Rec.Modify();
            end;
        }
        """,
        """
        codeunit 1 Test
        {
            procedure Do(NewRec: Record "New Table")
            begin
                NewRec.Modify();
            end;
        }
        """,
        "cloud"
    )]
    // non-var param: var added by new name
    [InlineData(
        """
        codeunit 1 Test
        {
            // @altools:transform:cloud:param:Rec:var Rec: Record "New Table"
            procedure Do(Rec: Record "Old Table")
            begin
                Rec.Modify();
            end;
        }
        """,
        """
        codeunit 1 Test
        {
            procedure Do(var Rec: Record "New Table")
            begin
                Rec.Modify();
            end;
        }
        """,
        "cloud"
    )]
    // Trigger parameters
    [InlineData(
        """
        codeunit 1 Test
        {
            // @altools:transform:cloud:param:Sender:NewSender: Codeunit "New"
            trigger OnRun(Sender: Codeunit "Old")
            begin
                Sender.Run();
            end;
        }
        """,
        """
        codeunit 1 Test
        {
            trigger OnRun(NewSender: Codeunit "New")
            begin
                NewSender.Run();
            end;
        }
        """,
        "cloud"
    )]
    // Inactive tag -> untouched
    [InlineData(
        """
        codeunit 1 Test
        {
            // @altools:transform:onprem:param:Qty: Decimal
            procedure Do(Qty: Integer)
            begin
            end;
        }
        """,
        """
        codeunit 1 Test
        {
            // @altools:transform:onprem:param:Qty: Decimal
            procedure Do(Qty: Integer)
            begin
            end;
        }
        """,
        "cloud"
    )]
    public void TransformParameterTest(string input, string expected, string? tags)
    {
        Assert.Equal(expected, Run(input, tags), ignoreAllWhiteSpace: true, ignoreLineEndingDifferences: true);
    }

    private static string Run(string input, string? tags)
    {
        var compilationUnit = SyntaxFactory.ParseCompilationUnit(input);
        var compilation = Compilation.Create("temp").AddSyntaxTrees(compilationUnit.SyntaxTree);
        var model = compilation.GetSemanticModel(compilationUnit.SyntaxTree);

        HashSet<string>? activeTags = null;
        if (tags != null)
        {
            activeTags = new HashSet<string>(
                tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                StringComparer.OrdinalIgnoreCase
            );
        }

        var rewriter = new TransformParam(activeTags);
        var context = rewriter.EmptyContext.WithModel(model);

        return rewriter.Rewrite(compilationUnit, ref context).ToFullString();
    }
}
