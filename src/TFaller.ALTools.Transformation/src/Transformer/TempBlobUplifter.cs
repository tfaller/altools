using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.Syntax;
using TFaller.ALTools.Transformation.Rewriter;

namespace TFaller.ALTools.Transformation.Transformer;

/// <summary>
/// Rewriter that uplifts the removed table "TempBlob" to the codeunit "Temp Blob".
///
/// Variables and parameters of type <c>Record TempBlob</c> become <c>Codeunit "Temp Blob"</c>
/// and their usages are rewritten, because the procedures differ:
///
/// <code>
/// V.Blob.CreateInStream(S[, Enc])   -> V.CreateInStream(S[, Enc])
/// V.Blob.CreateOutStream(S[, Enc])  -> V.CreateOutStream(S[, Enc])
/// V.Blob.HasValue()                 -> V.HasValue()
/// V.Blob.Length()                   -> V.Length()
/// V.ToBase64String()                -> Base64Convert.ToBase64(V.CreateInStream())
/// V.FromBase64String(B)             -> Base64Convert.FromBase64(B, V.CreateOutStream())
/// V.WriteAsText(C[, Enc])           -> V.CreateOutStream([Enc]).WriteText(C)
/// V.ReadAsText(Sep[, Enc])          -> TypeHelper.ReadAsTextWithSeparator(V.CreateInStream([Enc]), Sep)
/// V.Init() / V.Delete() / V.DeleteAll() -> Clear(V)
/// V.Insert() / V.Modify() / V.Reset() / V.CalcFields(Blob) -> removed
/// </code>
///
/// Helper codeunit variables ("Base64 Convert", "Type Helper") are added to the enclosing
/// procedure/trigger if needed. Usages without an equivalent are left untouched, so the
/// compiler points them out afterwards.
///
/// The old table does not exist in current symbols, so everything is recognized syntactically
/// (type name + the variable symbols resolved by the semantic model).
/// </summary>
public class TempBlobUplifter : SyntaxRewriter, IReuseableRewriter
{
    private const string OldTableName = "TempBlob";
    private const string TempBlobCodeunitName = "Temp Blob";

    private static readonly HelperCodeunit Base64Convert = new("Base64Convert", "Base64 Convert");
    private static readonly HelperCodeunit TypeHelper = new("TypeHelper", "Type Helper");

    /// <summary>Blob field procedures that exist 1:1 on the codeunit.</summary>
    private static readonly HashSet<string> BlobPassThrough = new(StringComparer.OrdinalIgnoreCase)
    {
        "CreateInStream", "CreateOutStream", "HasValue", "Length"
    };

    /// <summary>Record procedures that have no meaning for the codeunit.</summary>
    private static readonly HashSet<string> RemovedStatements = new(StringComparer.OrdinalIgnoreCase)
    {
        "Insert", "Modify", "Reset"
    };

    /// <summary>Record procedures that reset the content.</summary>
    private static readonly HashSet<string> ClearStatements = new(StringComparer.OrdinalIgnoreCase)
    {
        "Init", "Delete", "DeleteAll"
    };

    private static readonly SyntaxTrivia Space = SyntaxFactory.WhiteSpace(" ");

    private static string Text(SyntaxToken token) => token.ValueText ?? string.Empty;

    private record HelperCodeunit(string DefaultName, string CodeunitName);

    private SemanticModel _model = null!;
    private HashSet<ISymbol> _targets = [];
    private string _eol = "\n";

    // State of the procedure/trigger that is currently rewritten
    private MethodOrTriggerDeclarationSyntax? _method;
    private Dictionary<HelperCodeunit, string> _helperNames = [];
    private List<(HelperCodeunit Helper, string Name)> _newHelpers = [];

    public SyntaxNode Rewrite(SyntaxNode node, ref IRewriterContext context)
    {
        _model = context.Model;
        _targets = CollectTargets(node);
        _method = null;

        if (_targets.Count == 0)
        {
            return node;
        }

        _eol = node.ToFullString().Contains("\r\n") ? "\r\n" : "\n";

        return Visit(node);
    }

    public IReuseableRewriter Clone() => new TempBlobUplifter();

    // No cross-file state needed: all usages of a variable are in the file that declares it.
    public IRewriterContext EmptyContext => new RewriterContext();

    public bool RerunUntilNoChanges => false;

    #region Declarations

    public override SyntaxNode VisitVariableDeclaration(VariableDeclarationSyntax node)
    {
        if (!IsTarget(node))
        {
            return base.VisitVariableDeclaration(node);
        }

        return node.WithType(NewType(node.Type));
    }

    public override SyntaxNode VisitParameter(ParameterSyntax node)
    {
        if (!IsTarget(node))
        {
            return base.VisitParameter(node);
        }

        return node.WithType(NewType(node.Type));
    }

    public override SyntaxNode VisitReturnValue(ReturnValueSyntax node)
    {
        if (node.Name is null || !IsTempBlobType(node.DataType) || !IsTarget(node))
        {
            return base.VisitReturnValue(node);
        }

        return node.WithDataType(NewType(node.DataType));
    }

    private static TypeReferenceBaseSyntax NewType(TypeReferenceBaseSyntax oldType)
    {
        return CodeunitType(TempBlobCodeunitName).WithTriviaFrom(oldType);
    }

    private HashSet<ISymbol> CollectTargets(SyntaxNode root)
    {
        var targets = new HashSet<ISymbol>();

        foreach (var node in root.DescendantNodes())
        {
            TypeReferenceBaseSyntax? type = node switch
            {
                VariableDeclarationSyntax v => v.Type,
                ParameterSyntax p => p.Type,
                ReturnValueSyntax { Name: not null } r => r.DataType,
                _ => null
            };

            if (type is null || !IsTempBlobType(type))
            {
                continue;
            }

            var symbol = node switch
            {
                VariableDeclarationSyntax v => _model.GetDeclaredSymbol(v),
                ParameterSyntax p => _model.GetDeclaredSymbol(p),
                ReturnValueSyntax r => _model.GetDeclaredSymbol(r),
                _ => null
            };

            if (symbol is not null)
            {
                targets.Add(symbol);
            }
        }

        return targets;
    }

    private static bool IsTempBlobType(TypeReferenceBaseSyntax type)
    {
        // Arrays of records are not uplifted automatically
        return type is RecordTypeReferenceSyntax { Array: null, DataType: SubtypedDataTypeSyntax { Subtype: { } subtype } } &&
            string.Equals(subtype.ToString().Trim().Trim('"'), OldTableName, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsTarget(SyntaxNode declaration)
    {
        var symbol = declaration switch
        {
            VariableDeclarationSyntax v => _model.GetDeclaredSymbol(v),
            ParameterSyntax p => _model.GetDeclaredSymbol(p),
            ReturnValueSyntax r => _model.GetDeclaredSymbol(r),
            _ => null
        };

        return symbol is not null && _targets.Contains(symbol);
    }

    /// <summary>True if the expression is a reference to an uplifted variable.</summary>
    private bool IsTargetReference(CodeExpressionSyntax? expression)
    {
        if (expression is not IdentifierNameSyntax identifier)
        {
            return false;
        }

        var symbol = _model.GetSymbolInfo(identifier).Symbol;
        return symbol is not null && _targets.Contains(symbol);
    }

    #endregion

    #region Procedures / Triggers

    public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        return VisitMethodOrTrigger(node, () => base.VisitMethodDeclaration(node)!);
    }

    public override SyntaxNode VisitTriggerDeclaration(TriggerDeclarationSyntax node)
    {
        return VisitMethodOrTrigger(node, () => base.VisitTriggerDeclaration(node)!);
    }

    private SyntaxNode VisitMethodOrTrigger(MethodOrTriggerDeclarationSyntax node, Func<SyntaxNode> visit)
    {
        _method = node;
        _helperNames = [];
        _newHelpers = [];

        var result = visit();

        var newHelpers = _newHelpers;
        _method = null;

        if (newHelpers.Count == 0)
        {
            return result;
        }

        return result switch
        {
            MethodDeclarationSyntax m => m.WithVariables(AddVariables(m.Variables, m.Body, newHelpers)),
            TriggerDeclarationSyntax t => t.WithVariables(AddVariables(t.Variables, t.Body, newHelpers)),
            _ => result
        };
    }

    /// <summary>
    /// Returns the name of the variable for a helper codeunit in the current procedure.
    /// Reuses an existing variable of that codeunit type, otherwise a new one is registered.
    /// Null if there is no enclosing procedure.
    /// </summary>
    private string? GetHelperName(HelperCodeunit helper)
    {
        if (_method is null)
        {
            return null;
        }

        if (_helperNames.TryGetValue(helper, out var known))
        {
            return known;
        }

        var scope = ScopeVariables(_method).ToList();

        var existing = scope.FirstOrDefault(v => IsCodeunitType(v.Type, helper.CodeunitName));
        if (existing is not null)
        {
            return _helperNames[helper] = Text(existing.Name.Identifier);
        }

        var used = scope.Select(v => Text(v.Name.Identifier))
            .Concat(_method.ParameterList?.Parameters.Select(p => Text(p.Name.Identifier)) ?? [])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var name = helper.DefaultName;
        for (var i = 2; used.Contains(name); i++)
        {
            name = helper.DefaultName + i;
        }

        _newHelpers.Add((helper, name));
        return _helperNames[helper] = name;
    }

    /// <summary>Variables visible in a procedure: its own and the global ones of the object.</summary>
    private static IEnumerable<VariableDeclarationSyntax> ScopeVariables(MethodOrTriggerDeclarationSyntax method)
    {
        var local = method.Variables?.Variables.OfType<VariableDeclarationSyntax>() ?? [];

        var objectNode = method.Ancestors().FirstOrDefault(a => a is ObjectSyntax or ControlAddInSyntax);
        var global = objectNode?.DescendantNodes(n => n is not MethodOrTriggerDeclarationSyntax)
            .OfType<VariableDeclarationSyntax>() ?? [];

        return local.Concat(global);
    }

    private static bool IsCodeunitType(TypeReferenceBaseSyntax type, string codeunitName)
    {
        var text = type.ToString().Trim();
        return text.StartsWith("Codeunit ", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(text["Codeunit ".Length..].Trim().Trim('"'), codeunitName, StringComparison.OrdinalIgnoreCase);
    }

    private VarSectionSyntax AddVariables(
        VarSectionSyntax? section,
        BlockSyntax? body,
        List<(HelperCodeunit Helper, string Name)> helpers)
    {
        if (section is not null && section.Variables.Count > 0)
        {
            // Same indentation as the last declaration
            var indent = LastWhitespace(section.Variables[^1].GetLeadingTrivia());
            var decls = helpers.Select(h => CreateVariable(h.Name, h.Helper.CodeunitName, indent));
            return section.WithVariables(section.Variables.AddRange(decls));
        }

        // "var" sits on the level of the "begin" keyword, the declarations are indented once more
        var varIndent = body is null ? "" : LastWhitespace(body.BeginKeywordToken.LeadingTrivia);
        var declIndent = varIndent + (varIndent.Contains('\t') ? "\t" : "    ");

        var newDecls = helpers.Select(h => CreateVariable(h.Name, h.Helper.CodeunitName, declIndent)).ToList();

        var varKeyword = SyntaxFactory.Token(SyntaxKind.VarKeyword)
            .WithLeadingTrivia(SyntaxFactory.WhiteSpace(varIndent))
            .WithTrailingTrivia(SyntaxFactory.EndOfLine(_eol));

        return SyntaxFactory.VarSection(varKeyword, SyntaxFactory.List<VariableDeclarationBaseSyntax>(newDecls));
    }

    private static string LastWhitespace(SyntaxTriviaList trivia)
    {
        return trivia.LastOrDefault(t => t.IsKind(SyntaxKind.WhiteSpaceTrivia)).ToString();
    }

    private VariableDeclarationSyntax CreateVariable(string name, string codeunitName, string indent)
    {
        return SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName(name), CodeunitType(codeunitName))
            .WithColonToken(SyntaxFactory.Token(SyntaxKind.ColonToken).WithTrailingTrivia(Space))
            .WithLeadingTrivia(SyntaxFactory.WhiteSpace(indent))
            .WithTrailingTrivia(SyntaxFactory.EndOfLine(_eol));
    }

    private static SimpleTypeReferenceSyntax CodeunitType(string codeunitName)
    {
        return SyntaxFactory.SimpleTypeReference(
            SyntaxFactory.SubtypedDataType(
                SyntaxFactory.ParseToken("Codeunit").WithTrailingTrivia(Space),
                SyntaxFactory.ObjectNameOrId(SyntaxFactory.IdentifierName(Formatter.QuoteIdentifier(codeunitName)))));
    }

    #endregion

    #region Usages

    public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
    {
        if (node.Expression is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } invocation &&
            IsTargetReference(access.Expression))
        {
            var method = Text(access.Name.Identifier);
            var variable = (IdentifierNameSyntax)access.Expression;

            if (ClearStatements.Contains(method) && invocation.ArgumentList.Arguments.Count == 0)
            {
                return node.WithExpression(Call("Clear", variable.WithoutTrivia()).WithTriviaFrom(node.Expression));
            }

            // Removing is only safe if the statement is part of a statement list
            var removable = node.Parent is BlockSyntax;
            if (removable && IsRemovedCall(method, invocation))
            {
                return null;
            }
        }

        return base.VisitExpressionStatement(node);
    }

    private static bool IsRemovedCall(string method, InvocationExpressionSyntax invocation)
    {
        if (RemovedStatements.Contains(method))
        {
            return true;
        }

        // The blob content is not stored in a field, so there is nothing to calculate
        return method.Equals("CalcFields", StringComparison.OrdinalIgnoreCase) &&
            invocation.ArgumentList.Arguments.Count > 0 &&
            invocation.ArgumentList.Arguments.All(a =>
                string.Equals(a.ToString().Trim().Trim('"'), "Blob", StringComparison.OrdinalIgnoreCase));
    }

    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (node.Expression is MemberAccessExpressionSyntax access)
        {
            var replacement = TryRewriteInvocation(node, access);
            if (replacement is not null)
            {
                return replacement;
            }
        }

        return base.VisitInvocationExpression(node);
    }

    private SyntaxNode? TryRewriteInvocation(InvocationExpressionSyntax node, MemberAccessExpressionSyntax access)
    {
        var method = Text(access.Name.Identifier);

        // V.Blob.Method(...)
        if (access.Expression is MemberAccessExpressionSyntax blobAccess &&
            IsBlobField(blobAccess) &&
            BlobPassThrough.Contains(method))
        {
            var arguments = (ArgumentListSyntax)Visit(node.ArgumentList)!;
            return node
                .WithExpression(access.WithExpression(blobAccess.Expression.WithTriviaFrom(blobAccess)))
                .WithArgumentList(arguments);
        }

        // V.Method(...)
        if (!IsTargetReference(access.Expression))
        {
            return null;
        }

        var variable = access.Expression.WithoutTrivia();
        var args = node.ArgumentList.Arguments
            .Select(a => ((CodeExpressionSyntax)Visit(a)!).WithoutTrivia())
            .ToList();

        CodeExpressionSyntax? result = null;

        switch (method.ToLowerInvariant())
        {
            case "tobase64string" when args.Count == 0 && GetHelperName(Base64Convert) is { } b64:
                result = Call(b64, "ToBase64", Call(variable, "CreateInStream"));
                break;

            case "frombase64string" when args.Count == 1 && GetHelperName(Base64Convert) is { } b64:
                result = Call(b64, "FromBase64", args[0], Call(variable, "CreateOutStream"));
                break;

            case "writeastext" when args.Count is 1 or 2:
                result = Call(Call(variable, "CreateOutStream", args.Skip(1).ToArray()), "WriteText", args[0]);
                break;

            case "readastext" when args.Count is 1 or 2 && GetHelperName(TypeHelper) is { } helper:
                result = Call(helper, "ReadAsTextWithSeparator", Call(variable, "CreateInStream", args.Skip(1).ToArray()), args[0]);
                break;
        }

        return result?.WithTriviaFrom(node);
    }

    public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        // V.Blob.HasValue / V.Blob.Length without parentheses
        if (node.Expression is MemberAccessExpressionSyntax blobAccess &&
            IsBlobField(blobAccess) &&
            BlobPassThrough.Contains(Text(node.Name.Identifier)))
        {
            return node.WithExpression(blobAccess.Expression.WithTriviaFrom(blobAccess));
        }

        return base.VisitMemberAccessExpression(node);
    }

    /// <summary>True for "V.Blob" where V is an uplifted variable.</summary>
    private bool IsBlobField(MemberAccessExpressionSyntax access)
    {
        return IsTargetReference(access.Expression) &&
            string.Equals(Text(access.Name.Identifier), "Blob", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Name(args)</summary>
    private static InvocationExpressionSyntax Call(string name, params CodeExpressionSyntax[] args)
    {
        return SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(name), ArgumentList(args));
    }

    /// <summary>target.Name(args)</summary>
    private static InvocationExpressionSyntax Call(CodeExpressionSyntax target, string name, params CodeExpressionSyntax[] args)
    {
        return SyntaxFactory.InvocationExpression(SyntaxFactory.MemberAccessExpression(target, name), ArgumentList(args));
    }

    /// <summary>target.Name(args), where the target is a variable name</summary>
    private static InvocationExpressionSyntax Call(string target, string name, params CodeExpressionSyntax[] args)
    {
        return Call(SyntaxFactory.IdentifierName(target), name, args);
    }

    private static ArgumentListSyntax ArgumentList(CodeExpressionSyntax[] args)
    {
        // Arguments are separated by ", "
        return SyntaxFactory.ArgumentList().AddArguments(
            [.. args.Select((a, i) => i == 0 ? a : a.WithLeadingTrivia(Space))]);
    }

    #endregion
}
