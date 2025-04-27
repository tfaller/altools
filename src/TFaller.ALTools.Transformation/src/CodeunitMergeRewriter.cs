using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace TFaller.ALTools.Transformation;

public class CodeunitMergeRewriter : SyntaxRewriter
{
    private readonly SemanticModel _model;
    private readonly HashSet<string> _codeunits;
    private readonly SimpleTypeReferenceSyntax _mergedCodeunitType;

    /// <summary>
    /// If true, symbols missing in the semantic model are ignored
    /// and throw no exception.
    /// </summary>
    public bool IgnoreMissingSymbols { get; set; }

    public CodeunitMergeRewriter(SemanticModel model, string mergedCodeunitName, HashSet<string> codeunits)
    {
        _model = model;
        _codeunits = codeunits;

        if (_codeunits.Comparer != StringComparer.InvariantCultureIgnoreCase)
        {
            _codeunits = new HashSet<string>(_codeunits, StringComparer.InvariantCultureIgnoreCase);
        }

        _mergedCodeunitType = SyntaxFactory.SimpleTypeReference(
            SyntaxFactory.SubtypedDataType(
                SyntaxFactory.Token(SyntaxKind.CodeunitKeyword),
                SyntaxFactory.ObjectNameOrId(SyntaxFactory.IdentifierName(mergedCodeunitName))
            )
        );
    }

    public static CodeunitSyntax Merge(string name, int id, params CodeunitSyntax[] codeunits)
    {
        var compilationUnit = SyntaxFactory.CompilationUnit().AddObjects(codeunits);
        var compilation = Compilation.Create("temp").AddSyntaxTrees(compilationUnit.SyntaxTree);
        var model = compilation.GetSemanticModel(compilationUnit.SyntaxTree);

        var rewriter = new CodeunitMergeRewriter(model, name, [.. codeunits.Select(c => c.Name.Identifier.Text)]);
        var rewritten = rewriter.Visit(compilationUnit.SyntaxTree.GetRoot());

        var merged = SyntaxFactory.Codeunit(SyntaxFactory.ObjectId(id), name);

        foreach (var obj in rewritten.SyntaxTree.GetCompilationUnitRoot().Objects)
        {
            merged = merged.AddMembers([.. obj.Members]);
        }

        return merged.NormalizeWhiteSpace();
    }

    public override SyntaxNode VisitPropertyList(PropertyListSyntax node)
    {
        // skip, not relevant
        return node;
    }

    public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
    {
        if (!GetSymbol(node, out var symbol))
        {
            return base.VisitIdentifierName(node);
        }

        var kind = symbol.Kind;

        if ((kind == SymbolKind.GlobalVariable || kind == SymbolKind.Method)
            && symbol.ContainingType is ICodeunitTypeSymbol codeunit
            && _codeunits.Contains(codeunit.Name))
        {
            node = node.WithIdentifier(SyntaxFactory.Identifier(codeunit.Name + "_" + symbol.Name));
        }

        return base.VisitIdentifierName(node);
    }

    public override SyntaxNode VisitVariableDeclaration(VariableDeclarationSyntax node)
    {
        var symbol = (IVariableSymbol?)_model.GetDeclaredSymbol(node);

        if (RewriteVariableType(symbol?.Type) is var newType && newType != null)
        {
            return node.WithType(newType);
        }

        return base.VisitVariableDeclaration(node);
    }

    public override SyntaxNode VisitParameter(ParameterSyntax node)
    {
        var symbol = (IParameterSymbol?)_model.GetDeclaredSymbol(node);

        if (RewriteVariableType(symbol?.ParameterType) is var newType && newType != null)
        {
            return node.WithType(newType);
        }

        return base.VisitParameter(node);
    }

    private SimpleTypeReferenceSyntax? RewriteVariableType(ITypeSymbol? type)
    {
        if (!(type is ICodeunitTypeSymbol codeunit && _codeunits.Contains(codeunit.Name)))
        {
            return null;
        }

        return _mergedCodeunitType;
    }

    private bool GetSymbol(SyntaxNode node, [NotNullWhen(true)] out ISymbol? symbol)
    {
        symbol = _model.GetSymbolInfo(node).Symbol;

        return symbol != null
            || (IgnoreMissingSymbols ? false : throw new InvalidOperationException("Symbol not found, invalid semantic model."));
    }
}