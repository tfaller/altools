using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.Syntax;

namespace TFaller.ALTools.Transformation;

/// <summary>
/// Transpiles the new support for complex returns into classic "var return"
/// </summary>
public class ComplexReturnTranspiler : SyntaxRewriter
{
    private string? _returnName;
    private readonly SyntaxToken _semicolon;
    private readonly ExitStatementSyntax _emptyExit;
    private readonly SemanticModel _model;

    public ComplexReturnTranspiler(SemanticModel model)
    {
        _model = model;
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

        node = node
        .WithReturnValue(null)
        .WithParameterList(node.ParameterList.AddParameters(
            SyntaxFactory.Parameter(
                _returnName = returnValue.Name?.Identifier.ValueText ?? "Return",
                simpleType
            ).WithVarKeyword(SyntaxFactory.Token(SyntaxKind.VarKeyword))
        ));

        var result = base.VisitMethodDeclaration(node);
        _returnName = null;
        return result;
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

        foreach (var stmt in node.Statements)
        {
            if (stmt is ExitStatementSyntax exit && exit.ExitValue is not null)
            {
                rewrittenList = rewrittenList
                .Add(
                    SyntaxFactory.AssignmentStatement(
                        SyntaxFactory.IdentifierName(_returnName),
                        exit.ExitValue
                    ).WithSemicolonToken(_semicolon)
                )
                .Add(_emptyExit);
            }
            else
            {
                rewrittenList = rewrittenList.Add(stmt);
            }
        }

        return node.WithStatements(rewrittenList);
    }

    public override SyntaxNode VisitAssignmentStatement(AssignmentStatementSyntax node)
    {
        if (node.Source is not InvocationExpressionSyntax source ||
            _model.GetSymbolInfo(source).Symbol is not IMethodSymbol symbol ||
            symbol.ReturnValueSymbol.ReturnType.Kind != SymbolKind.Codeunit)
        {
            // not an invocation, nothing to do
            return base.VisitAssignmentStatement(node);
        }

        return SyntaxFactory
            .ExpressionStatement(source.AddArgumentListArguments(node.Target))
            .WithSemicolonToken(node.SemicolonToken);
    }
}