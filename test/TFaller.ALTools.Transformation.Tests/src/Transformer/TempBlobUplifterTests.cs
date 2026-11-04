using Microsoft.Dynamics.Nav.CodeAnalysis;
using TFaller.ALTools.Transformation.Transformer;

namespace TFaller.ALTools.Transformation.Tests;

public class TempBlobUplifterTests
{
    [Theory]
    // Declaration + blob field streams + Init/Insert handling
    [InlineData(
        """
        codeunit 1 Test
        {
            procedure Do()
            var
                TempBlob: Record TempBlob temporary;
                OutStr: OutStream;
                InStr: InStream;
            begin
                TempBlob.Init();
                TempBlob.Blob.CreateOutStream(OutStr, TextEncoding::UTF8);
                OutStr.WriteText('x');
                TempBlob.Insert();
                TempBlob.CalcFields(Blob);
                TempBlob.Blob.CreateInStream(InStr);
                if TempBlob.Blob.HasValue() then
                    Message('%1', TempBlob.Blob.Length());
            end;
        }
        """,
        """
        codeunit 1 Test
        {
            procedure Do()
            var
                TempBlob: Codeunit "Temp Blob";
                OutStr: OutStream;
                InStr: InStream;
            begin
                Clear(TempBlob);
                TempBlob.CreateOutStream(OutStr, TextEncoding::UTF8);
                OutStr.WriteText('x');
                TempBlob.CreateInStream(InStr);
                if TempBlob.HasValue() then
                    Message('%1', TempBlob.Length());
            end;
        }
        """
    )]
    // Quoted table name, uppercase procedure names, no parentheses
    [InlineData(
        """
        codeunit 1 Test
        {
            procedure Do()
            var
                TempBlob: Record "TempBlob";
            begin
                TempBlob.Blob.CREATEOUTSTREAM(OutStr);
                if TempBlob.Blob.HasValue then
                    exit;
            end;
        }
        """,
        """
        codeunit 1 Test
        {
            procedure Do()
            var
                TempBlob: Codeunit "Temp Blob";
            begin
                TempBlob.CREATEOUTSTREAM(OutStr);
                if TempBlob.HasValue then
                    exit;
            end;
        }
        """
    )]
    // Parameters (var and by value), including the callers' argument
    [InlineData(
        """
        codeunit 1 Test
        {
            procedure Fill(var Blob: Record TempBlob temporary; Source: Record TempBlob)
            begin
                Blob.Blob.CreateOutStream(OutStr);
            end;

            procedure Caller()
            var
                TempBlob: Record TempBlob;
            begin
                Fill(TempBlob, TempBlob);
            end;
        }
        """,
        """
        codeunit 1 Test
        {
            procedure Fill(var Blob: Codeunit "Temp Blob"; Source: Codeunit "Temp Blob")
            begin
                Blob.CreateOutStream(OutStr);
            end;

            procedure Caller()
            var
                TempBlob: Codeunit "Temp Blob";
            begin
                Fill(TempBlob, TempBlob);
            end;
        }
        """
    )]
    // Global variable used in a procedure declared before the variable
    [InlineData(
        """
        codeunit 1 Test
        {
            procedure Do()
            begin
                GlobalBlob.Blob.CreateInStream(InStr);
            end;

            var
                GlobalBlob: Record TempBlob temporary;
        }
        """,
        """
        codeunit 1 Test
        {
            procedure Do()
            begin
                GlobalBlob.CreateInStream(InStr);
            end;

            var
                GlobalBlob: Codeunit "Temp Blob";
        }
        """
    )]
    // Base64 + text procedures, helper variables are added to an existing var section
    [InlineData(
        """
        codeunit 1 Test
        {
            procedure Do(): Text
            var
                TempBlob: Record TempBlob temporary;
                Result: Text;
            begin
                TempBlob.FromBase64String(Input);
                Result := TempBlob.ToBase64String();
                TempBlob.WriteAsText('abc', TextEncoding::UTF8);
                exit(TempBlob.ReadAsText('', TextEncoding::UTF8));
            end;
        }
        """,
        """
        codeunit 1 Test
        {
            procedure Do(): Text
            var
                TempBlob: Codeunit "Temp Blob";
                Result: Text;
                Base64Convert: Codeunit "Base64 Convert";
                TypeHelper: Codeunit "Type Helper";
            begin
                Base64Convert.FromBase64(Input, TempBlob.CreateOutStream());
                Result := Base64Convert.ToBase64(TempBlob.CreateInStream());
                TempBlob.CreateOutStream(TextEncoding::UTF8).WriteText('abc');
                exit(TypeHelper.ReadAsTextWithSeparator(TempBlob.CreateInStream(TextEncoding::UTF8), ''));
            end;
        }
        """
    )]
    // Procedure without var section gets one; the helper is shared by all usages
    [InlineData(
        """
        codeunit 1 Test
        {
            procedure Do(var TempBlob: Record TempBlob temporary)
            begin
                Response.Add('a', TempBlob.ToBase64String());
                Response.Add('b', TempBlob.ToBase64String());
            end;
        }
        """,
        """
        codeunit 1 Test
        {
            procedure Do(var TempBlob: Codeunit "Temp Blob")
            var
                Base64Convert: Codeunit "Base64 Convert";
            begin
                Response.Add('a', Base64Convert.ToBase64(TempBlob.CreateInStream()));
                Response.Add('b', Base64Convert.ToBase64(TempBlob.CreateInStream()));
            end;
        }
        """
    )]
    // An existing helper variable (global) is reused
    [InlineData(
        """
        codeunit 1 Test
        {
            procedure Reuse(var TempBlob: Record TempBlob temporary)
            begin
                Response.Add('a', TempBlob.ToBase64String());
            end;

            var
                B64: Codeunit "Base64 Convert";
        }
        """,
        """
        codeunit 1 Test
        {
            procedure Reuse(var TempBlob: Codeunit "Temp Blob")
            begin
                Response.Add('a', B64.ToBase64(TempBlob.CreateInStream()));
            end;

            var
                B64: Codeunit "Base64 Convert";
        }
        """
    )]
    // A name clash with an unrelated variable gets a new name
    [InlineData(
        """
        codeunit 1 Test
        {
            procedure Clash(var TempBlob: Record TempBlob temporary)
            var
                Base64Convert: Text;
            begin
                Response.Add('a', TempBlob.ToBase64String());
            end;
        }
        """,
        """
        codeunit 1 Test
        {
            procedure Clash(var TempBlob: Codeunit "Temp Blob")
            var
                Base64Convert: Text;
                Base64Convert2: Codeunit "Base64 Convert";
            begin
                Response.Add('a', Base64Convert2.ToBase64(TempBlob.CreateInStream()));
            end;
        }
        """
    )]
    // Triggers
    [InlineData(
        """
        codeunit 1 Test
        {
            trigger OnRun()
            var
                TempBlob: Record TempBlob temporary;
            begin
                Text := TempBlob.ToBase64String();
            end;
        }
        """,
        """
        codeunit 1 Test
        {
            trigger OnRun()
            var
                TempBlob: Codeunit "Temp Blob";
                Base64Convert: Codeunit "Base64 Convert";
            begin
                Text := Base64Convert.ToBase64(TempBlob.CreateInStream());
            end;
        }
        """
    )]
    // Unsupported usages and other records are untouched, statements that can't be removed stay
    [InlineData(
        """
        codeunit 1 Test
        {
            procedure Do()
            var
                TempBlob: Record TempBlob temporary;
                Other: Record "Sales Header";
            begin
                Other.Insert();
                Other.Blob.CreateOutStream(OutStr);
                TempBlob.Blob.Import('file.txt');
                if X then
                    TempBlob.Insert();
            end;
        }
        """,
        """
        codeunit 1 Test
        {
            procedure Do()
            var
                TempBlob: Codeunit "Temp Blob";
                Other: Record "Sales Header";
            begin
                Other.Insert();
                Other.Blob.CreateOutStream(OutStr);
                TempBlob.Blob.Import('file.txt');
                if X then
                    TempBlob.Insert();
            end;
        }
        """
    )]
    // No TempBlob at all -> unchanged
    [InlineData(
        """
        codeunit 1 Test
        {
            procedure Do()
            var
                Rec: Record "Sales Header";
            begin
                Rec.Insert();
            end;
        }
        """,
        """
        codeunit 1 Test
        {
            procedure Do()
            var
                Rec: Record "Sales Header";
            begin
                Rec.Insert();
            end;
        }
        """
    )]
    public void UpliftTempBlobTest(string input, string expected)
    {
        Assert.Equal(expected, Run(input), ignoreLineEndingDifferences: true);
    }

    private static string Run(string input)
    {
        var compilationUnit = SyntaxFactory.ParseCompilationUnit(input);
        var compilation = Compilation.Create("temp").AddSyntaxTrees(compilationUnit.SyntaxTree);
        var model = compilation.GetSemanticModel(compilationUnit.SyntaxTree);

        var rewriter = new TempBlobUplifter();
        var context = rewriter.EmptyContext.WithModel(model);

        return rewriter.Rewrite(compilationUnit, ref context).ToFullString();
    }
}
