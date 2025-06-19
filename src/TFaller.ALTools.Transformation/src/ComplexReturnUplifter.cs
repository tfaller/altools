using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.Syntax;
using TFaller.ALTools.Transformation.Rewriter;

namespace TFaller.ALTools.Transformation;

using Context = RewriterContextWithState<HashSet<IMethodSymbol>>;

/// <summary>
/// Uplifts a method var codeunit parameter to a complex return value.
/// The method must not have already a return value.
/// The paremter must be first be assigned or cleared, before otherwise used.
/// </summary>
public class ComplexReturnUplifter : SyntaxRewriter, IReuseableRewriter
{
    private Context _context = null!;
    private SemanticModel _model = null!;
    private HashSet<IMethodSymbol> _upliftedMethods = null!;
    private bool _firstRun = false;
    private readonly HashSet<SyntaxTree> _dependencies = [];
    private readonly SyntaxToken _closeParenthesisToken = SyntaxFactory.Token(SyntaxKind.CloseParenToken);
    private readonly SyntaxToken _openParenthesisToken = SyntaxFactory.Token(SyntaxKind.OpenParenToken);
    private readonly SyntaxToken _semicolon = SyntaxFactory.Token(SyntaxKind.SemicolonToken);
    private string? _returnName;
    private bool _returnInitialized;
    private bool _returnUsedBeforeInitialization;
    private bool _returnUsed;

    public SyntaxNode Rewrite(SyntaxNode node, ref IRewriterContext context)
    {
        _dependencies.Clear();

        _context = (Context)context;
        _model = context.Model;
        _firstRun = _context.State == null;
        _upliftedMethods = _context.State ?? [];

        var rewritten = Visit(node);

        if (!_firstRun)
        {
            // make sure we dont run again
            _dependencies.Clear();
        }
        else
        {
            if (_upliftedMethods.Count > 0)
            {
                // we have to visit this tree again, to rewrite in second run
                _dependencies.Add(node.SyntaxTree);
            }
        }

        context = _context
            .WithState(_upliftedMethods)
            .WithDependencies(_dependencies);

        // make sure we return the original node in the first run
        // otherwise the model would be broken the next time we run
        return _firstRun ? node : rewritten;
    }

    public IReuseableRewriter Clone()
    {
        return new ComplexReturnUplifter();
    }

    public IRewriterContext EmptyContext => new Context();

    public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (node.ReturnValue is not null || // has already a return value
            node.Attributes.Any(a => "EventSubscriber".EqualsOrdinalIgnoreCase(a.Name.Identifier.ValueText)) ||
            node.ParameterList.Parameters.Count == 0 ||
            node.ParameterList.Parameters[^1] is not ParameterSyntax lastParameter ||
            lastParameter.VarKeyword.Kind != SyntaxKind.VarKeyword ||
            lastParameter.Type is not SimpleTypeReferenceSyntax simpleType ||
            simpleType.DataType is not SubtypedDataTypeSyntax subtypedType ||
            subtypedType.TypeName.Kind != SyntaxKind.CodeunitKeyword)
        {
            // method does not return a codeunit ... not complex return
            return base.VisitMethodDeclaration(node);
        }

        _returnName = lastParameter.Name.Identifier.ValueText;
        _returnInitialized = false;
        _returnUsedBeforeInitialization = false;
        _returnUsed = false;

        // save our own symbol now, breaks if some rewriter changes our method syntax 
        var methodSymbol = (IMethodSymbol)_model.GetDeclaredSymbol(node)!;

        node = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node);

        if ((_returnInitialized && !_returnUsedBeforeInitialization) || !_returnUsed)
        {
            var parameterList = node.ParameterList;
            var parameters = parameterList.Parameters;

            node = node
                .WithParameterList(
                    node.ParameterList
                    .WithParameters(
                        parameters.RemoveAt(parameters.Count - 1)
                    )
                    .WithoutTrailingTrivia()
                )
                .WithReturnValue(
                    SyntaxFactory.ReturnValue(lastParameter.Name, lastParameter.Type)
                    .WithTrailingTrivia(parameterList.GetTrailingTrivia())
                );

            // we actually uplifted the method, note it so call sites can be rewritten
            _upliftedMethods.Add(methodSymbol);
        }

        // we are not anymore in rewrite procdure
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

        var rewrittenList = new List<StatementSyntax>(node.Statements.Count);
        var changedList = false;

        foreach (var stmt in node.Statements)
        {
            var newStmt = stmt;

            if (stmt is ExitStatementSyntax exit &&
                exit.ExitValue is null &&
                rewrittenList.Count > 0 &&
                rewrittenList[^1] is AssignmentStatementSyntax assignment &&
                assignment.Target is IdentifierNameSyntax ident &&
                string.Equals(ident.Identifier.ValueText, _returnName, StringComparison.CurrentCultureIgnoreCase))
            {
                // swap the assigment and exit stmts with a single exit stmt

                rewrittenList.Remove(assignment);

                newStmt = exit
                    .WithExitValue(assignment.Source)
                    .WithOpenParenthesisToken(_openParenthesisToken)
                    .WithCloseParenthesisToken(_closeParenthesisToken);

                changedList = true;
            }

            rewrittenList.Add(newStmt);
        }

        return changedList ? node.WithStatements(SyntaxFactory.List(rewrittenList)) : node;
    }

    public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (node.Expression is not IdentifierNameSyntax ident ||
            !string.Equals(ident.Identifier.ValueText, "Clear") ||
            node.ArgumentList.Arguments.Count != 1 ||
            node.ArgumentList.Arguments[0] is not IdentifierNameSyntax argIdent ||
            !string.Equals(argIdent.Identifier.ValueText, _returnName, StringComparison.CurrentCultureIgnoreCase))
        {
            // not a clear, basic flow
            return base.VisitInvocationExpression(node);
        }

        // the return parameter was intialized by Clear()
        _returnInitialized = true;

        return node;
    }

    public override SyntaxNode VisitAssignmentStatement(AssignmentStatementSyntax node)
    {
        if (node.Target is not IdentifierNameSyntax ident ||
            !string.Equals(ident.Identifier.ValueText, _returnName, StringComparison.CurrentCultureIgnoreCase))
        {
            // not an assignment to the return value
            return base.VisitAssignmentStatement(node);
        }

        // the return parameter was intialized by assignment
        _returnInitialized = true;

        return node;
    }

    public override SyntaxNode VisitParameterList(ParameterListSyntax node)
    {
        // we don't have to visit parameters
        return node;
    }

    public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
    {
        // determine the usage of the return parameter
        if (_model.GetSymbolInfo(node).Symbol is IParameterSymbol param &&
            param.Name.EqualsOrdinalIgnoreCase(_returnName))
        {
            _returnUsed = true;

            if (!_returnInitialized)
            {
                _returnUsedBeforeInitialization = true;
            }
        }

        return node;
    }

    public override SyntaxNode VisitExpressionStatement(ExpressionStatementSyntax node)
    {
        // Rewrite procedure calls that return a codeunit as assignment statements
        // instead of passing the codeunit as var parameter.

        node = (ExpressionStatementSyntax)base.VisitExpressionStatement(node);

        if (node.Expression is not InvocationExpressionSyntax invocation ||
            invocation.ArgumentList is not ArgumentListSyntax argumentList ||
            argumentList.Arguments.Count == 0 ||
            _model.GetSymbolInfo(invocation.Expression).Symbol is not IMethodSymbol method ||
            method.DeclaringSyntaxReference is not SyntaxReference reference)
        {
            // not an invocation, or not a method we uplifted
            return node;
        }

        var refSyntaxTree = reference.SyntaxTree;

        if (!_upliftedMethods.Contains(method) &&
            !(_context.TryGetContext(refSyntaxTree, out var refContext) && refContext.State.Contains(method)))
        {
            // maybe gets rewritten later
            _dependencies.Add(refSyntaxTree);
            return node;
        }

        var target = argumentList.Arguments[^1];

        invocation = invocation.WithArgumentList(
            argumentList.WithArguments(argumentList.Arguments.Remove(target))
        );

        return SyntaxFactory
            .AssignmentStatement(target, invocation)
            .WithSemicolonToken(_semicolon)
            .WithTriviaFrom(node);
    }
}