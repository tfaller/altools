using System;
using System.Collections.Generic;
using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.Syntax;
using TFaller.ALTools.Transformation.Rewriter;

namespace TFaller.ALTools.Transformation;

using Context = RewriterContextWithState<HashSet<IMethodSymbol>>;

/// <summary>
/// Transpiles the new support for complex returns into classic "var return"
/// </summary>
public class ComplexReturnTranspiler : SyntaxRewriter, IReuseableRewriter
{
    private bool _firstRun = false;
    private Context _context = null!;
    private string? _returnName;
    private ISymbol? _returnSymbol;
    private HashSet<IMethodSymbol> _transpiledMethods = null!;
    private SemanticModel _model = null!;
    private readonly HashSet<SyntaxTree> _dependencies = [];
    private readonly SyntaxToken _semicolon;
    private readonly SyntaxToken _varKeyword = SyntaxFactory.Token(SyntaxKind.VarKeyword);
    private readonly ExitStatementSyntax _emptyExit;
    private readonly VarUsageAnalyzer _varUsageAnalyzer = new(null!);

    public IRewriterContext EmptyContext => new Context();

    public bool RerunUntilNoChanges => false;

    public ComplexReturnTranspiler()
    {
        _semicolon = SyntaxFactory.Token(SyntaxKind.SemicolonToken);
        _emptyExit = SyntaxFactory.ExitStatement().WithSemicolonToken(_semicolon);
    }

    public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (node.ReturnValue is var returnValue && returnValue is null ||
            returnValue.DataType is not SimpleTypeReferenceSyntax simpleType ||
            simpleType.DataType is not SubtypedDataTypeSyntax subtypedType ||
            subtypedType.TypeName.Kind != SyntaxKind.CodeunitKeyword)
        {
            // method does not return a codeunit ... not complex return
            return base.VisitMethodDeclaration(node);
        }

        _transpiledMethods.Add((IMethodSymbol)(_model.GetDeclaredSymbol(node)
            ?? throw new InvalidOperationException("Method symbol not found")));

        _returnSymbol = _model.GetDeclaredSymbol(returnValue)!;
        _varUsageAnalyzer.Watch(_returnSymbol);
        _returnName = returnValue.Name?.Identifier.ValueText ?? "Return";

        node = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node);

        node = node
        .WithReturnValue(null)
        .WithParameterList(
            node.ParameterList.AddParameters(
                _semicolon,
                SyntaxFactory.Parameter(
                    _returnName,
                    simpleType.WithoutTrailingTrivia()
                ).WithVarKeyword(_varKeyword)
            ).WithTrailingTrivia(simpleType.GetTrailingTrivia())
        );

        _returnName = null;
        return node;
    }

    public override SyntaxNode VisitBlock(BlockSyntax node)
    {
        if (_returnName is null)
        {
            // no complex return, nothing to do
            return base.VisitBlock(node);
        }

        node = (BlockSyntax)base.VisitBlock(node);
        var rewrittenList = new SyntaxList<StatementSyntax>();

        if (node.Parent is MethodDeclarationSyntax)
        {
            var usage = _varUsageAnalyzer.GetUsage(_returnSymbol!);
            if ((usage & VarUsageAnalyzer.Usage.Initialized) == 0)
            {
                // make sure the parameter is cleared
                rewrittenList = rewrittenList.Add(
                     SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.InvocationExpression(
                            SyntaxFactory.IdentifierName("Clear"),
                            SyntaxFactory.ArgumentList().AddArguments(SyntaxFactory.IdentifierName(_returnName))
                        ),
                        _semicolon
                     ).WithTrailingTrivia(SyntaxFactory.CarriageReturnLinefeed)
                );
            }
        }

        foreach (var stmt in node.Statements)
        {
            if (stmt is ExitStatementSyntax exit && exit.ExitValue is not null)
            {
                rewrittenList = rewrittenList
                .Add(
                    SyntaxFactory.AssignmentStatement(
                        SyntaxFactory.IdentifierName(_returnName),
                        exit.ExitValue
                    )
                    .WithSemicolonToken(_semicolon)
                    .WithTrailingTrivia(SyntaxFactory.CarriageReturnLinefeed)
                )
                .Add(_emptyExit.WithTrailingTrivia(exit.GetTrailingTrivia()));
            }
            else
            {
                rewrittenList = rewrittenList.Add(stmt);
            }
        }

        return node.WithStatements(rewrittenList);
    }

    public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        _varUsageAnalyzer.VisitInvocationExpression(node);
        return base.VisitInvocationExpression(node);
    }

    public override SyntaxNode VisitAssignmentStatement(AssignmentStatementSyntax node)
    {
        _varUsageAnalyzer.VisitAssignmentStatement(node);

        if (node.Source is not InvocationExpressionSyntax source ||
            _model.GetSymbolInfo(source).Symbol is not IMethodSymbol symbol ||
            symbol.DeclaringSyntaxReference?.SyntaxTree is not SyntaxTree syntaxReference)
        {
            // not an invocation, nothing to do
            return node;
        }

        if (!_transpiledMethods.Contains(symbol) &&
            !(_context.TryGetContext(syntaxReference, out var context) && context.State.Contains(symbol)))
        {
            _dependencies.Add(syntaxReference);
            return node;
        }

        return SyntaxFactory
            .ExpressionStatement(source.AddArgumentListArguments(node.Target.WithoutTrivia()))
            .WithSemicolonToken(node.SemicolonToken)
            .WithTriviaFrom(node);
    }

    public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
    {
        _varUsageAnalyzer.VisitIdentifierName(node);
        return base.VisitIdentifierName(node);
    }

    public override SyntaxNode VisitExitStatement(ExitStatementSyntax node)
    {
        var result = base.VisitExitStatement(node);

        if (_returnSymbol is not null && node.ExitValue is not null)
        {
            _varUsageAnalyzer.Initialized(_returnSymbol);
        }

        return result;
    }

    public SyntaxNode Rewrite(SyntaxNode node, ref IRewriterContext context)
    {
        _dependencies.Clear();

        _context = (Context)context;
        _model = _context.Model;
        _firstRun = _context.State is null;
        _transpiledMethods = _context.State ?? [];
        _varUsageAnalyzer.Clear(_model);

        var result = Visit(node);

        if (!_firstRun)
        {
            _dependencies.Clear();
        }
        else
        {
            if (_transpiledMethods.Count > 0 || _dependencies.Count > 0)
            {
                _dependencies.Add(node.SyntaxTree);
            }
        }

        context = _context
            .WithState(_transpiledMethods)
            .WithDependencies(_dependencies);

        return _firstRun ? node : result;
    }

    public IReuseableRewriter Clone()
    {
        return new ComplexReturnTranspiler();
    }
}