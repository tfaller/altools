using Microsoft.Dynamics.Nav.CodeAnalysis;
using System;
using System.Collections.Generic;
using TFaller.ALTools.Transformation.Transformer.CommentRule;

namespace TFaller.ALTools.Transformation.Tests;

public class TransformVarTests
{
    [Theory]
    // Basic variable rename with type change
    [InlineData(
        """
        codeunit 1 Test
        {
            procedure CancelActiveResource(ContractNo: Text; LineNo: Integer): Boolean
            var
                // @altools:transform:cloud:var:NewSalesLine: Record "New Sales Line"
                SalesLine: Record "Sales Line";
            begin
                if not SalesLine.Get(SalesLine."Document Type"::"Blanket Order", ContractNo, LineNo) then
                    Exit(false);
                
                SalesLine.Validate("Ending Date", Today());
                SalesLine.Modify(true);
                
                exit(true);
            end;
        }
        """,
        """
        codeunit 1 Test
        {
            procedure CancelActiveResource(ContractNo: Text; LineNo: Integer): Boolean
            var
                NewSalesLine: Record "New Sales Line";
            begin
                if not NewSalesLine.Get(NewSalesLine."Document Type"::"Blanket Order", ContractNo, LineNo) then
                    Exit(false);
                
                NewSalesLine.Validate("Ending Date", Today());
                NewSalesLine.Modify(true);
                
                exit(true);
            end;
        }
        """,
        "cloud"
    )]
    // Multiple variables with different tags - only matching tag transformed
    [InlineData(
        """
        codeunit 1 Test
        {
            procedure DoWork()
            var
                // @altools:transform:cloud:var:CloudVar: Text[100]
                LocalVar1: Text[50];
                // @altools:transform:onprem:var:OnPremVar: Integer
                LocalVar2: Text[20];
            begin
                LocalVar1 := 'test';
                LocalVar2 := 'value';
            end;
        }
        """,
        """
        codeunit 1 Test
        {
            procedure DoWork()
            var
                CloudVar: Text[100];
                // @altools:transform:onprem:var:OnPremVar: Integer
                LocalVar2: Text[20];
            begin
                CloudVar := 'test';
                LocalVar2 := 'value';
            end;
        }
        """,
        "cloud"
    )]
    // No tag specified - transform all
    [InlineData(
        """
        codeunit 1 Test
        {
            procedure DoWork()
            var
                // @altools:transform:cloud:var:CloudVar: Text[100]
                LocalVar1: Text[50];
                // @altools:transform:onprem:var:OnPremVar: Integer
                LocalVar2: Text[20];
            begin
                LocalVar1 := 'test';
                LocalVar2 := 'value';
            end;
        }
        """,
        """
        codeunit 1 Test
        {
            procedure DoWork()
            var
                CloudVar: Text[100];
                OnPremVar: Integer;
            begin
                CloudVar := 'test';
                OnPremVar := 'value';
            end;
        }
        """,
        null
    )]
    // Variable without transform comment - unchanged
    [InlineData(
        """
        codeunit 1 Test
        {
            procedure DoWork()
            var
                RegularVar: Text[50];
                // @altools:transform:cloud:var:CloudVar: Text[100]
                LocalVar: Text[20];
            begin
                RegularVar := 'test';
                LocalVar := 'value';
            end;
        }
        """,
        """
        codeunit 1 Test
        {
            procedure DoWork()
            var
                RegularVar: Text[50];
                CloudVar: Text[100];
            begin
                RegularVar := 'test';
                CloudVar := 'value';
            end;
        }
        """,
        "cloud"
    )]
    // Complex type transformation - Codeunit
    [InlineData(
        """
        codeunit 1 Test
        {
            procedure DoWork()
            var
                // @altools:transform:cloud:var:CloudCodeunit: Codeunit "Cloud Handler"
                LocalCodeunit: Codeunit "Local Handler";
            begin
                LocalCodeunit.Process();
            end;
        }
        """,
        """
        codeunit 1 Test
        {
            procedure DoWork()
            var
                CloudCodeunit: Codeunit "Cloud Handler";
            begin
                CloudCodeunit.Process();
            end;
        }
        """,
        "cloud"
    )]
    // Variable used in multiple places
    [InlineData(
        """
        codeunit 1 Test
        {
            procedure DoWork()
            var
                // @altools:transform:cloud:var:TempRec: Record "Temp Table"
                MyRec: Record "My Table";
            begin
                MyRec.Init();
                MyRec."Field 1" := 'test';
                MyRec.Insert();
                if MyRec.Get('test') then
                    MyRec.Delete();
            end;
        }
        """,
        """
        codeunit 1 Test
        {
            procedure DoWork()
            var
                TempRec: Record "Temp Table";
            begin
                TempRec.Init();
                TempRec."Field 1" := 'test';
                TempRec.Insert();
                if TempRec.Get('test') then
                    TempRec.Delete();
            end;
        }
        """,
        "cloud"
    )]
    // Malformed comment - missing colon after tag (should be ignored)
    [InlineData(
        """
        codeunit 1 Test
        {
            procedure DoWork()
            var
                // @altools:transformcloud:var:NewVar: Text[100]
                OldVar: Text[50];
            begin
                OldVar := 'test';
            end;
        }
        """,
        """
        codeunit 1 Test
        {
            procedure DoWork()
            var
                // @altools:transformcloud:var:NewVar: Text[100]
                OldVar: Text[50];
            begin
                OldVar := 'test';
            end;
        }
        """,
        "cloud"
    )]
    // Multiple tags active
    [InlineData(
        """
        codeunit 1 Test
        {
            procedure DoWork()
            var
                // @altools:transform:cloud:var:CloudVar: Text[100]
                LocalVar1: Text[50];
                // @altools:transform:saas:var:SaasVar: Integer
                LocalVar2: Text[20];
                // @altools:transform:onprem:var:OnPremVar: Decimal
                LocalVar3: Text[30];
            begin
                LocalVar1 := 'test1';
                LocalVar2 := 'test2';
                LocalVar3 := 'test3';
            end;
        }
        """,
        """
        codeunit 1 Test
        {
            procedure DoWork()
            var
                CloudVar: Text[100];
                SaasVar: Integer;
                // @altools:transform:onprem:var:OnPremVar: Decimal
                LocalVar3: Text[30];
            begin
                CloudVar := 'test1';
                SaasVar := 'test2';
                LocalVar3 := 'test3';
            end;
        }
        """,
        "cloud,saas"
    )]
    // Transform with simple type
    [InlineData(
        """
        codeunit 1 Test
        {
            procedure DoWork()
            var
                // @altools:transform:cloud:var:NewInt: Integer
                OldInt: Decimal;
            begin
                OldInt := 42;
            end;
        }
        """,
        """
        codeunit 1 Test
        {
            procedure DoWork()
            var
                NewInt: Integer;
            begin
                NewInt := 42;
            end;
        }
        """,
        "cloud"
    )]
    // Variable scoping - only local scope affected
    [InlineData(
        """
        codeunit 1 Test
        {
            var
                GlobalVar: Text[50];
                
            procedure DoWork()
            var
                // @altools:transform:cloud:var:CloudVar: Text[100]
                LocalVar: Text[50];
            begin
                LocalVar := 'test';
                GlobalVar := 'global';
            end;
        }
        """,
        """
        codeunit 1 Test
        {
            var
                GlobalVar: Text[50];
                
            procedure DoWork()
            var
                CloudVar: Text[100];
            begin
                CloudVar := 'test';
                GlobalVar := 'global';
            end;
        }
        """,
        "cloud"
    )]
    public void TransformVariableTest(string input, string expected, string? tags)
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

        var rewriter = new TransformVar(activeTags);
        var context = rewriter.EmptyContext.WithModel(model);

        var result = rewriter.Rewrite(compilationUnit, ref context);

        Assert.Equal(expected, result.ToFullString(), ignoreAllWhiteSpace: true, ignoreLineEndingDifferences: true);
    }
}
