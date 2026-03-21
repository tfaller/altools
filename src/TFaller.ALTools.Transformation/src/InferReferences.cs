using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.Packaging;
using Microsoft.Dynamics.Nav.CodeAnalysis.SymbolReference;
using Microsoft.Dynamics.Nav.CodeAnalysis.Symbols;
using Microsoft.Dynamics.Nav.CodeAnalysis.Syntax;
using Microsoft.Dynamics.Nav.CodeAnalysis.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TFaller.ALTools.Transformation;

/// <summary>
/// Infers missing type references from AL workspace source code by analyzing undefined references
/// and inferring their structure based on usage patterns (method calls, field access, etc.).
/// </summary>
public static class InferReferences
{
    internal const string DummyType = "Variant";

    private static readonly HashSet<string> _reservedRecordMethods = new([
        "CalcFields",
        "ChangeCompany",
        "Count",
        "CurrentCompany",
        "FieldNo",
        "SetCurrentKey",
        "SetFilter",
        "SetRange",
        "IsEmpty",
        "IsTemporary",
        "FieldError",
        "Find",
        "FindFirst",
        "FindLast",
        "FindSet",
        "Get",
        "GetFilter",
        "Init",
        "Next",
        "Insert",
        "Modify",
        "Delete",
        "TestField",
    ], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Analyzes an AL workspace and generates a ModuleDefinition containing inferred types
    /// for all missing/undefined references found in the code.
    /// </summary>
    /// <param name="workspacePath">Path to the AL workspace directory</param>
    /// <param name="moduleName">Name of the module to generate</param>
    /// <param name="modulePublisher">Publisher of the module</param>
    /// <param name="moduleVersion">Version of the module (e.g., "1.0.0.0")</param>
    /// <param name="moduleId">Optional GUID for the module</param>
    /// <returns>A ModuleDefinition containing all inferred types</returns>
    public static async Task<ModuleDefinition> InferFromWorkspace(
        string workspacePath,
        string moduleName,
        string modulePublisher,
        string moduleVersion,
        Guid? moduleId = null)
    {
        if (!Directory.Exists(workspacePath))
        {
            throw new ArgumentException($"Workspace path does not exist: {workspacePath}", nameof(workspacePath));
        }

        Console.WriteLine($"Analyzing workspace: {workspacePath}");
        Console.WriteLine($"Creating module: {moduleName} by {modulePublisher} v{moduleVersion}");

        // Create compilation
        var compilation = Compilation.Create(moduleName);
        var files = new Dictionary<string, SyntaxTree>();

        // Load all AL files from the workspace
        Console.WriteLine("Loading AL files...");
        compilation = await WorkspaceHelper.LoadFilesAsync(compilation, workspacePath, null!, files);
        Console.WriteLine($"Loaded {files.Count} AL files");

        compilation = compilation.WithReferenceManager(ReferenceManagerFactory.Create([], compilation));

        // Analyze undefined references and infer types
        Console.WriteLine("\nAnalyzing undefined references...");
        var moduleDefinition = await AnalyzeUndefinedReferences(compilation, files);

        // Build inferred type definitions
        Console.WriteLine("\nInferring type structures from usage...");

        return moduleDefinition;
    }

    private static void InferFiles(Compilation compilation, Dictionary<string, SyntaxTree> files, Dictionary<string, InferredTypeInfo> inferredTypes)
    {
        foreach (var syntaxTree in files.Values)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var analyzer = new UsageAnalyzer(semanticModel, inferredTypes);
            analyzer.Visit(syntaxTree.GetRoot());
        }
    }

    /// <summary>
    /// Analyzes the compilation for undefined references and builds inferred type information
    /// based on how these types are used in the code.
    /// </summary>
    private static async Task<ModuleDefinition> AnalyzeUndefinedReferences(
        Compilation compilation,
        Dictionary<string, SyntaxTree> files)
    {
        var module = new ModuleDefinition()
        {
            AppId = Guid.NewGuid(),
        };

        var inferredTypes = new Dictionary<string, InferredTypeInfo>();

        // First pass
        InferFiles(compilation, files, inferredTypes);
        UpdateModuleDefinition(module, inferredTypes, false);

        // Last pass to finalize types, we then will already know some inferred types and can "recursively" infer more types

        compilation = compilation.WithReferenceLoader(new StaticReferenceLoader(module));
        compilation = compilation.WithReferences([new SymbolReferenceSpecification("tmp", "objects", new Version(), false, module.AppId)]);

        InferFiles(compilation, files, inferredTypes);
        UpdateModuleDefinition(module, inferredTypes, true);

        return module;
    }

    private static void UpdateModuleDefinition(ModuleDefinition module, Dictionary<string, InferredTypeInfo> inferredTypes, bool final)
    {
        var tableId = 1;
        var codeunitId = 1;
        var reportId = 1;
        var xmlportId = 1;

        // Clear existing definitions, otherwise we would duplicate entries
        module.Tables = [];
        module.Codeunits = [];
        module.Pages = [];
        module.Reports = [];
        module.XmlPorts = [];

        foreach (var t in inferredTypes.Values)
        {
            var fieldId = 1;

            foreach (var f in t.Fields.Values)
            {
                if (f.Type == DummyType)
                {
                    if (final)
                    {
                        f.Type = "Variant";
                    }
                    else
                    {
                        t.Fields.Remove(f.Name!);
                        continue;
                    }
                }

                f.Id = fieldId++;
                f.TypeDefinition = null; // Remove our type hints
            }

            if (t.Kind == SyntaxKind.CodeunitKeyword)
            {
                var codeunit = new CodeunitDefinition
                {
                    Id = codeunitId++,
                    Name = t.Name,
                    Methods = [.. t.Methods],
                };
                module.Codeunits = [.. module.Codeunits ?? [], codeunit];
            }

            if (t.Kind == SyntaxKind.TableKeyword)
            {
                var table = new TableDefinition
                {
                    Id = tableId++,
                    Name = t.Name,
                    Fields = [.. t.Fields.Values],
                    Methods = [.. t.Methods.Where(m => !_reservedRecordMethods.Contains(m.Name!))],
                };
                module.Tables = [.. module.Tables ?? [], table];
            }

            if (t.Kind == SyntaxKind.ReportKeyword)
            {
                var report = new ReportDefinition
                {
                    Id = reportId++,
                    Name = t.Name,
                    Methods = [.. t.Methods],
                };
                module.Reports = [.. module.Reports ?? [], report];
            }

            if (t.Kind == SyntaxKind.XmlPortKeyword)
            {
                var xmlport = new XmlPortDefinition
                {
                    Id = xmlportId++,
                    Name = t.Name,
                    Methods = [.. t.Methods],
                };
                module.XmlPorts = [.. module.XmlPorts ?? [], xmlport];
            }

            if (t.Kind == SyntaxKind.PageKeyword)
            {
                var page = new PageDefinition
                {
                    Id = tableId++, // use table id for pages as well
                    Name = t.Name,
                    Methods = [.. t.Methods],
                };
                module.Pages = [.. module.Pages ?? [], page];
            }
        }
    }

    /// <summary>
    /// Generates reference definitions from a workspace and saves them to a .app package file.
    /// </summary>
    /// <param name="workspacePath">Path to the AL workspace directory</param>
    /// <param name="outputPath">Path where the .app package should be saved</param>
    /// <param name="moduleName">Name of the module to generate</param>
    /// <param name="modulePublisher">Publisher of the module</param>
    /// <param name="moduleVersion">Version of the module (e.g., "1.0.0.0")</param>
    /// <param name="moduleId">Optional GUID for the module</param>
    public static async Task GenerateReferencePackage(
        string workspacePath,
        string outputPath,
        string moduleName,
        string modulePublisher,
        string moduleVersion,
        Guid? moduleId = null)
    {
        moduleId ??= Guid.NewGuid();

        // Infer references from the workspace
        var moduleDefinition = await InferFromWorkspace(
            workspacePath,
            moduleName,
            modulePublisher,
            moduleVersion,
            moduleId);

        // Create the output directory if it doesn't exist
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        // Create a new app package
        Console.WriteLine($"Creating app package: {outputPath}");

        var manifest = new NavAppManifest
        {
            AppId = moduleId.Value,
            AppVersion = Version.Parse("14.0.0.0"),
            AppName = moduleName,
            AppPublisher = modulePublisher,
        };

        // Create the package
        using (var stream = File.Create(outputPath))
        using (var package = NavAppPackage.Create(stream, moduleId.Value, false))
        using (var writer = new NavAppPackageWriter(package))
        {
            writer.WriteManifest(manifest);
            writer.WriteModule(moduleDefinition);
        }

        Console.WriteLine($"Successfully created reference package: {outputPath}");
    }

    /// <summary>
    /// Exports the module definition to a JSON file for inspection.
    /// </summary>
    /// <param name="moduleDefinition">The module definition to export</param>
    /// <param name="outputPath">Path where the JSON file should be saved</param>
    public static void ExportToJson(ModuleDefinition moduleDefinition, string outputPath)
    {
        Console.WriteLine($"Exporting module definition to: {outputPath}");

        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        using (var stream = File.Create(outputPath))
        {
            SymbolReferenceJsonWriter.WriteModule(stream, moduleDefinition);
        }

        Console.WriteLine($"Successfully exported to JSON");
    }
}

/// <summary>
/// Stores information about an inferred type based on its usage in code.
/// </summary>
internal class InferredTypeInfo
{
    public string Name { get; set; } = "";
    public SyntaxKind Kind { get; set; } = SyntaxKind.CodeunitKeyword; // Codeunit, Table, Page, etc.
    public int? ObjectId { get; set; }
    public List<MethodDefinition> Methods { get; } = [];
    public Dictionary<string, FieldDefinition> Fields { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> UsageLocations { get; } = []; // For tracking where it's used
    public int NewMembersCount { get; set; } = 0;

    public FieldDefinition Field(string name)
    {
        name = Formatter.UnquoteIdentifier(name);

        if (!Fields.TryGetValue(name, out var field))
        {
            NewMembersCount++;

            Fields.Add(name, field = new FieldDefinition
            {
                Name = name,
                Type = InferReferences.DummyType,
            });
        }

        return field;
    }
}

/// <summary>
/// Syntax walker that analyzes how undefined types are used in the code.
/// </summary>
internal class UsageAnalyzer : SyntaxWalker
{
    private static readonly HashSet<string> _textMethods = new([
        "Contains",
        "Replace",
        "StartsWith",
        "Split",
        "Trim",
        "ToLower",
    ], StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> _blobMethods = new([
        "CreateInStream",
        "CreateOutStream",
        "HasValue",
    ], StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<SyntaxKind> _inferByBinaryOperand = [
        SyntaxKind.EqualsToken,
        SyntaxKind.LessThanToken,
        SyntaxKind.LessThanEqualsToken,
        SyntaxKind.GreaterThanToken,
        SyntaxKind.GreaterThanEqualsToken,
        SyntaxKind.NotEqualsToken,
        SyntaxKind.PlusToken,
        SyntaxKind.MinusToken,
        SyntaxKind.MultiplyToken,
        SyntaxKind.RDivToken,
    ];

    private readonly SemanticModel _semanticModel;
    private readonly Dictionary<string, InferredTypeInfo> _inferredTypes = [];
    private readonly Dictionary<ISymbol, InferredTypeInfo> _symbolToInferredType = [];

    public UsageAnalyzer(SemanticModel semanticModel, Dictionary<string, InferredTypeInfo> inferredTypes)
    {
        _semanticModel = semanticModel;
        _inferredTypes = inferredTypes;
    }

    public override void VisitPage(PageSyntax node)
    {
        var sourceTableProperty = node.PropertyList.Properties
            .OfType<PropertySyntax>()
            .SingleOrDefault(p => p.Name.Identifier.Text.EqualsOrdinalIgnoreCase("SourceTable"));

        if (sourceTableProperty != null)
        {
            var name = Formatter.UnquoteIdentifier(((IdentifierNameSyntax)((ObjectReferencePropertyValueSyntax)sourceTableProperty.Value).ObjectNameOrId.Identifier).Identifier.Text);
            var pageSymbol = (IPageTypeSymbol)_semanticModel.GetDeclaredSymbol(node)!;

            if (pageSymbol.RelatedTable == null)
            {
                // We need to infer the table
                EnsureTypeExists(name, SyntaxKind.TableKeyword);
            }
            else if (_inferredTypes.ContainsKey(name))
            {
                // We might already have symbols now, but we might still can infer more information#
                var recSymbol = pageSymbol.GetMembers("Rec").OfType<IVariableSymbol>().Single();
                _symbolToInferredType.Add(recSymbol, EnsureTypeExists(name, SyntaxKind.TableKeyword));
            }
        }

        base.VisitPage(node);
    }

    public override void VisitVariableDeclaration(VariableDeclarationSyntax node)
    {
        if (node.Type.DataType is not SubtypedDataTypeSyntax typeRef)
        {
            return;
        }

        var typeName = ExtractTypeName(typeRef);
        var typeKind = ExtractTypeKind(typeRef);

        // Check if this variable has an undefined type
        var symbol = (IVariableSymbol)_semanticModel.GetDeclaredSymbol(node)!;
        if (symbol.Type.Kind == SymbolKind.ErrorType || _inferredTypes.ContainsKey(typeName))
        {
            // This is an undefined type - extract type information
            _symbolToInferredType.TryAdd(symbol, EnsureTypeExists(typeName, typeKind));
        }
    }

    public override void VisitParameter(ParameterSyntax node)
    {
        if (node.Type.DataType is not SubtypedDataTypeSyntax typeRef)
        {
            return;
        }

        var typeName = ExtractTypeName(typeRef);
        var typeKind = ExtractTypeKind(typeRef);

        var symbol = (IParameterSymbol)_semanticModel.GetDeclaredSymbol(node)!;
        if (symbol.ParameterType.Kind == SymbolKind.ErrorType || _inferredTypes.ContainsKey(typeName))
        {
            _symbolToInferredType.Add(symbol, EnsureTypeExists(typeName, typeKind));
        }
    }

    public override void VisitXmlPortTableElement(XmlPortTableElementSyntax node)
    {
        HandleXmlPortTableElement(node);
        base.VisitXmlPortTableElement(node);
    }

    private void HandleXmlPortTableElement(XmlPortTableElementSyntax node)
    {
        var typeName = ((IdentifierNameSyntax)node.SourceTable.Identifier).Identifier.Text;

        var symbol = (IXmlPortNodeSymbol)_semanticModel.GetDeclaredSymbol(node)!;
        if (IsUnkownType(symbol.GetTypeSymbol()) || _inferredTypes.ContainsKey(typeName))
        {
            _symbolToInferredType.TryAdd(symbol, EnsureTypeExists(typeName, SyntaxKind.TableKeyword));
        }
    }

    private static bool IsUnkownType(ITypeSymbol typeSymbol)
    {
        return typeSymbol.Kind == SymbolKind.ErrorType || typeSymbol.NavTypeKind == NavTypeKind.None;
    }

    public InferredTypeInfo? EnsureIfNeeded(ISymbol symbol)
    {
        if (_symbolToInferredType.TryGetValue(symbol, out InferredTypeInfo? value))
        {
            return value;
        }

        if (symbol.DeclaringSyntaxReference == null)
        {
            return null;
        }

        var syntax = symbol.DeclaringSyntaxReference.GetSyntax();

        if (syntax is VariableDeclarationSyntax varDecl)
        {
            VisitVariableDeclaration(varDecl);
        }
        else if (syntax is ParameterSyntax paramDecl)
        {
            VisitParameter(paramDecl);
        }
        else if (syntax is XmlPortTableElementSyntax xmlPortTableElement)
        {
            HandleXmlPortTableElement(xmlPortTableElement);
        }

        if (_symbolToInferredType.TryGetValue(symbol, out value))
        {
            return value;
        }

        return null;
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        // Check if this is a method call on an undefined type
        var symbolInfo = _semanticModel.GetSymbolInfo(node);

        if (symbolInfo.Symbol == null && node.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            // Try to determine the type of the expression before the member access
            var expressionSymbol = _semanticModel.GetSymbolInfo(memberAccess.Expression).Symbol;

            if (expressionSymbol is not null)
            {
                var methodName = memberAccess.Name.Identifier.Text;
                var typeInfo = EnsureIfNeeded(expressionSymbol);
                if (typeInfo != null)
                {
                    RecordMethodUsage(typeInfo, methodName, node.ArgumentList.Arguments, node);
                }
            }
        }

        base.VisitInvocationExpression(node);
    }

    public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        // For sure go deeper, however, we can do this before we handle the member access
        base.VisitMemberAccessExpression(node);

        if (node.Parent is InvocationExpressionSyntax)
        {
            // Don't add a method as a field again
            return;
        }

        // Check if this is a field/property access on an undefined type
        var symbolInfo = _semanticModel.GetSymbolInfo(node);

        if (symbolInfo.Symbol == null)
        {
            var expressionSymbol = _semanticModel.GetSymbolInfo(node.Expression).Symbol;
            var fieldName = node.Name.Identifier.Text;

            if (expressionSymbol is not null)
            {

                var typeInfo = EnsureIfNeeded(expressionSymbol);
                if (typeInfo != null)
                {
                    RecordFieldUsage(typeInfo, fieldName, node);
                }
            }
        }
    }

    private InferredTypeInfo EnsureTypeExists(string typeName, SyntaxKind kind)
    {
        ArgumentException.ThrowIfNullOrEmpty(typeName);

        if (typeName.StartsWith('"'))
            typeName = Formatter.UnquoteIdentifier(typeName);

        if (!_inferredTypes.TryGetValue(typeName, out InferredTypeInfo? value))
        {
            _inferredTypes[typeName] = value = new InferredTypeInfo
            {
                Name = typeName,
                Kind = kind
            };
        }
        return value;
    }

    private static string GetValidateFieldName(CodeExpressionSyntax expression)
    {
        if (expression is MemberAccessExpressionSyntax memberAccess)
        {
            expression = memberAccess.Name;
        }

        if (expression is IdentifierNameSyntax ident)
        {
            return ident.Identifier.Text;
        }

        throw new InvalidOperationException("Unable to extract field name from expression.");
    }

    private void RecordMethodUsage(InferredTypeInfo typeInfo, string methodName, SeparatedSyntaxList<CodeExpressionSyntax>? arguments, InvocationExpressionSyntax node)
    {
        if (typeInfo.Kind == SyntaxKind.TableKeyword)
        {
            if (methodName.EqualsOrdinalIgnoreCase("Validate") && arguments?.Count > 0)
            {
                // special validate method -> this assigns a field value
                var field = typeInfo.Field(GetValidateFieldName(arguments.Value[0]));
                if (arguments?.Count > 1)
                {
                    InferFieldType(field, arguments.Value[1]);
                }
                return;
            }

            if (methodName.EqualsOrdinalIgnoreCase("TestField") && arguments?.Count > 0)
            {
                // special TestField method -> tests a field value
                var field = typeInfo.Field(GetValidateFieldName(arguments.Value[0]));
                if (arguments?.Count > 1)
                {
                    InferFieldType(field, arguments.Value[1]);
                }
                return;
            }

            if (methodName.EqualsOrdinalIgnoreCase("SetRange") && arguments?.Count > 1)
            {
                // special SetRange method -> filters a field
                var field = typeInfo.Field(GetValidateFieldName(arguments.Value[0]));
                for (var i = 1; i < arguments.Value.Count; i++)
                {
                    // infer for each argument in the range, could improve type inference
                    InferFieldType(field, arguments!.Value[i]);
                }
                return;
            }

            if (methodName.EqualsOrdinalIgnoreCase("SetFilter") && arguments?.Count > 2)
            {
                // special SetFilter method -> filters a field
                var field = typeInfo.Field(GetValidateFieldName(arguments.Value[0]));
                for (var i = 2; i < arguments.Value.Count; i++)
                {
                    // infer for each argument in the filter, could improve type inference
                    InferFieldType(field, arguments!.Value[i]);
                }
                return;
            }

            if ((methodName.EqualsOrdinalIgnoreCase("SetCurrentKey") || methodName.EqualsOrdinalIgnoreCase("CalcFields")) && arguments?.Count > 0)
            {
                // special SetCurrentKey and CalcFields method -> list fields
                foreach (var field in arguments.Value)
                {
                    typeInfo.Field(GetValidateFieldName(field));
                }
                return;
            }

            if (methodName.EqualsOrdinalIgnoreCase("FieldNo") && arguments?.Count > 0)
            {
                // special method that gets the field number, we can just "declare" this field as existing
                typeInfo.Field(GetValidateFieldName(arguments.Value[0]));
                return;
            }

            if (methodName.EqualsOrdinalIgnoreCase("FieldError") && arguments?.Count > 0)
            {
                // special method that throws an error for a field, we can just "declare" this field as existing
                typeInfo.Field(GetValidateFieldName(arguments.Value[0]));
                return;
            }

            if (methodName.EqualsOrdinalIgnoreCase("GetFilter") && arguments?.Count > 0)
            {
                // special method that gets the current filter of a field, we can just "declare" this field as existing
                typeInfo.Field(GetValidateFieldName(arguments.Value[0]));
                return;
            }
        }

        // Check if we already have this method
        var existingMethod = typeInfo.Methods.FirstOrDefault(m =>
            string.Equals(m.Name, methodName, StringComparison.OrdinalIgnoreCase));

        if (existingMethod == null)
        {
            var args = new List<ParameterDefinition>();
            foreach (var arg in arguments ?? [])
            {
                args.Add(new ParameterDefinition
                {
                    Name = $"param{args.Count + 1}",
                    TypeDefinition = InferTypeFromExpression(arg),
                });
            }

            var method = existingMethod = new MethodDefinition
            {
                Name = methodName,
                Parameters = [.. args],
            };

            typeInfo.Methods.Add(method);
        }

        var dummyField = new FieldDefinition
        {
            Name = "Dummy",
            Type = existingMethod.ReturnType ?? InferReferences.DummyType,
        };

        RecordFieldUsage(dummyField, node);

        existingMethod.ReturnType = dummyField.Type;
    }

    private void RecordFieldUsage(InferredTypeInfo typeInfo, string fieldName, MemberAccessExpressionSyntax node)
    {
        var existingField = typeInfo.Field(fieldName);
        RecordFieldUsage(existingField, node);
    }

    private static string AddOptionMember(string? existingMembers, string newMember)
    {
        if (string.IsNullOrWhiteSpace(existingMembers))
        {
            return newMember;
        }

        return existingMembers
            .SplitCommaSeparatedIdentifierList()
            .Select(Formatter.UnquoteIdentifier)
            .Union([Formatter.UnquoteIdentifier(newMember)])
            .Select(Formatter.QuoteIdentifier)
            .Join(",");
    }

    private void RecordFieldUsage(FieldDefinition existingField, CodeExpressionSyntax node)
    {
        var fieldName = existingField.Name;

        if (node.Parent is ParenthesizedExpressionSyntax paren)
        {
            // Unwrap parentheses. So that from now on we work with the whole expression
            // instead of just the inner part. This is fine, because there should not happen a
            // variable symbole lookup anymore.
            node = paren;
        }

        // try to refine the type based on usage
        if (node.Parent is OptionAccessExpressionSyntax opt)
        {
            if (existingField.Type != InferReferences.DummyType && existingField.Type != "Option" && existingField.Type != "Integer")
            {
                Console.Error.WriteLine($"Warning: Conflicting type usage for field {fieldName}, existing type: {existingField.Type}, inferred Option usage.");
            }

            var member = ((IdentifierNameSyntax)opt.Name).Identifier.Text;
            existingField.Type = "Option";
            existingField.OptionMembers = AddOptionMember(existingField.OptionMembers, member);
        }

        if (node.Parent is MemberAccessExpressionSyntax memberAccess)
        {
            if (existingField.Type == InferReferences.DummyType && _textMethods.Contains(memberAccess.Name.Identifier.Text))
            {
                // This has to be text, complex fields can't be nested.
                // It could, if the field is actually a procedure, but we hope that all precedures are called with parentheses.
                existingField.Type = "Text";
            }

            if (existingField.Type == InferReferences.DummyType && _blobMethods.Contains(memberAccess.Name.Identifier.Text))
            {
                // This has to be text, complex fields can't be nested.
                // It could, if the field is actually a procedure, but we hope that all precedures are called with parentheses.
                existingField.Type = "Blob";
            }
        }

        if (node.Parent is BinaryExpressionSyntax binaryExpr
            && binaryExpr.OperatorToken is SyntaxToken op
            && (op.Kind == SyntaxKind.AndKeyword || op.Kind == SyntaxKind.OrKeyword))
        {
            // We have to be a boolean, otherwise AND, OR makes no sense
            existingField.Type = "Boolean";
        }

        if (node.Parent is BinaryExpressionSyntax compExpress && _inferByBinaryOperand.Contains(compExpress.OperatorToken.Kind))
        {
            var otherValue = compExpress.Left == node ? compExpress.Right : compExpress.Left;
            InferFieldType(existingField, otherValue);
        }

        if (node.Parent is AssignmentStatementSyntax assign)
        {
            InferAssignment(existingField, node, assign.Target, assign.Source);
        }

        if (node.Parent is CompoundAssignmentStatementSyntax compoundAssignment)
        {
            InferAssignment(existingField, node, compoundAssignment.Target, compoundAssignment.Source);
        }
    }

    private void InferAssignment(FieldDefinition field, SyntaxNode node, SyntaxNode target, SyntaxNode source)
    {
        if (source == node)
        {
            var symbol = _semanticModel.GetSymbolInfo(target).Symbol;
            if (symbol != null)
            {
                InferFieldType(field, symbol.GetTypeSymbol());
            }
        }
        else
        {
            InferFieldType(field, source);
        }
    }

    private void InferFieldType(FieldDefinition field, SyntaxNode node)
    {
        var operation = _semanticModel.GetOperation(node);

        if (operation is not null && operation.Type.Kind != SymbolKind.ErrorType)
        {
            InferFieldType(field, operation.Type);
        }
    }

    private static void InferFieldType(FieldDefinition field, ITypeSymbol type)
    {
        if (type.Kind == SymbolKind.ErrorType)
        {
            // can't infer from an undefined type
            return;
        }

        if (type.NavTypeKind == NavTypeKind.None)
        {
            // Not clear when exactly it is a ErrorType or a None type, but sure it is not inferrable
            // Just ignore it like the ErrorType
            return;
        }

        if (type.NavTypeKind == NavTypeKind.Joker)
        {
            // Joker type is useless for us, improves nothing
            return;
        }

        if (type.NavTypeKind == NavTypeKind.Integer && "Option".EqualsOrdinalIgnoreCase(field.Type))
        {
            // Options are integers, don't overwrite option -> integer
            return;
        }

        if (type.NavTypeKind == NavTypeKind.Char)
        {
            // Char is special, it can be seen as a Text subset
            // Char -> Text: Allowed
            // Text -> Char: Not allowed, keep as Text

            if (InferReferences.DummyType.EqualsOrdinalIgnoreCase(field.Type) || "Char".EqualsOrdinalIgnoreCase(field.Type))
            {
                field.Type = type.Name;
            }
            else if (!"Text".EqualsOrdinalIgnoreCase(field.Type))
            {
                Console.Error.WriteLine($"Warning: Conflicting type usage for field {field.Name}, existing type: {field.Type}, inferred type: {type.Name}.");
            }
        }
        else
        {
            field.Type = type.Name;
        }
    }

    private TypeDefinition InferTypeFromExpression(ExpressionSyntax expression)
    {
        return new TypeDefinition
        {
            Name = "Variant"
        };
    }

    private string ExtractTypeName(SubtypedDataTypeSyntax subtyped)
    {

        if (subtyped.Subtype is ObjectNameOrIdSyntax nameOrId)
        {
            if (nameOrId.Identifier is IdentifierNameSyntax identifier)
            {
                return Formatter.UnquoteIdentifier(identifier.Identifier.Text);
            }
        }

        return "";
    }

    private SyntaxKind ExtractTypeKind(SubtypedDataTypeSyntax subtyped)
    {
        return subtyped.TypeName.ValueText!.ToLowerInvariant() switch
        {
            "codeunit" => SyntaxKind.CodeunitKeyword,
            "record" => SyntaxKind.TableKeyword,
            "page" => SyntaxKind.PageKeyword,
            "report" => SyntaxKind.ReportKeyword,
            "query" => SyntaxKind.QueryKeyword,
            "xmlport" => SyntaxKind.XmlPortKeyword,
            "dotnet" => SyntaxKind.DotNetKeyword,
            _ => throw new InvalidOperationException($"Unknown type name: {subtyped.TypeName.Value}"),
        };
    }
}