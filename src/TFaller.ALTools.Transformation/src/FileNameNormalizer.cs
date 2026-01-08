using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.Syntax;

namespace TFaller.ALTools.Transformation;

/// <summary>
/// Normalizes AL file names to follow Microsoft's best practices: ObjectName.FullTypeName.al
/// </summary>
public static class FileNameNormalizer
{
    public static async Task<Dictionary<string, string>> AnalyzeWorkspace(string workspace, ParseOptions parseOptions)
    {
        var comp = Compilation.Create("tmp");
        var files = new Dictionary<string, SyntaxTree>();

        comp = WorkspaceHelper.LoadReferences(comp, workspace + "/.alpackages");
        comp = await WorkspaceHelper.LoadFilesAsync(comp, workspace, parseOptions, files);

        var fileRenames = new Dictionary<string, string>();

        foreach (var kvp in files)
        {
            var currentFilePath = kvp.Key;
            var syntaxTree = kvp.Value;
            var root = await syntaxTree.GetRootAsync();

            if (root is not CompilationUnitSyntax compilationUnit)
                continue;

            if (compilationUnit.Objects.Count > 1)
            {
                Console.WriteLine($"Skipping {Path.GetFileName(currentFilePath)}: contains multiple objects");
                continue;
            }

            var objectDeclaration = compilationUnit.Objects.FirstOrDefault();
            if (objectDeclaration == null)
                continue;

            var objectType = GetObjectType(objectDeclaration);
            if (string.IsNullOrEmpty(objectType))
                continue;

            var objectName = GetObjectName(objectDeclaration);
            if (string.IsNullOrEmpty(objectName))
                continue;

            var expectedFileName = $"{SanitizeFileName(objectName)}.{objectType}.al";
            var currentFileName = Path.GetFileName(currentFilePath);

            if (!string.Equals(currentFileName, expectedFileName, StringComparison.OrdinalIgnoreCase))
            {
                var directory = Path.GetDirectoryName(currentFilePath);
                var newFilePath = Path.Combine(directory ?? "", expectedFileName);

                if (!File.Exists(newFilePath))
                {
                    fileRenames[currentFilePath] = newFilePath;
                }
            }
        }

        return fileRenames;
    }

    public static void PerformRenames(Dictionary<string, string> fileRenames)
    {
        foreach (var kvp in fileRenames)
        {
            var oldPath = kvp.Key;
            var newPath = kvp.Value;

            if (File.Exists(oldPath) && !File.Exists(newPath))
            {
                var directory = Path.GetDirectoryName(newPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.Move(oldPath, newPath);
            }
        }
    }

    private static string GetObjectType(SyntaxNode objectDeclaration)
    {
        return objectDeclaration switch
        {
            TableSyntax => "Table",
            TableExtensionSyntax => "TableExt",
            PageSyntax => "Page",
            PageExtensionSyntax => "PageExt",
            CodeunitSyntax => "Codeunit",
            ReportSyntax => "Report",
            ReportExtensionSyntax => "ReportExt",
            QuerySyntax => "Query",
            XmlPortSyntax => "Xmlport",
            ProfileSyntax => "Profile",
            PageCustomizationSyntax => "PageCust",
            ControlAddInSyntax => "ControlAddin",
            InterfaceSyntax => "Interface",
            PermissionSetSyntax => "PermissionSet",
            PermissionSetExtensionSyntax => "PermissionSetExt",
            EntitlementSyntax => "Entitlement",
            _ => ""
        };
    }

    private static string GetObjectName(SyntaxNode objectDeclaration)
    {
        var name = objectDeclaration switch
        {
            TableSyntax table => table.Name,
            TableExtensionSyntax tableExt => tableExt.Name,
            PageSyntax page => page.Name,
            PageExtensionSyntax pageExt => pageExt.Name,
            CodeunitSyntax codeunit => codeunit.Name,
            ReportSyntax report => report.Name,
            ReportExtensionSyntax reportExt => reportExt.Name,
            QuerySyntax query => query.Name,
            XmlPortSyntax xmlPort => xmlPort.Name,
            ProfileSyntax profile => profile.Name,
            PageCustomizationSyntax pageCustom => pageCustom.Name,
            ControlAddInSyntax controlAddIn => controlAddIn.Name,
            InterfaceSyntax interfaceDecl => interfaceDecl.Name,
            PermissionSetSyntax permissionSet => permissionSet.Name,
            PermissionSetExtensionSyntax permissionSetExt => permissionSetExt.Name,
            EntitlementSyntax entitlement => entitlement.Name,
            _ => null
        };

        return name?.Identifier.ValueText ?? "";
    }

    private static string SanitizeFileName(string name)
    {
        name = Formatter.UnquoteIdentifier(name).Trim();

        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return name;
    }
}