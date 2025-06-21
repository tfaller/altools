using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.Syntax;

namespace TFaller.ALTools.Transformation;

public class VarUsageAnalyzer(SemanticModel model)
{
    public enum Usage : int
    {
        /// <summary>
        /// The symbol is not used in the code at all.
        /// </summary>
        Unused = 0,

        /// <summary>
        /// The symbol is used in the code.
        /// </summary>
        Used = 1,

        /// <summary>
        /// The symbol got explicitly initialized in the code before any usage.
        /// This can happen in two ways:
        /// - the symbol is assigned a value
        /// - Clear() is called on the symbol
        /// </summary>
        Initialized = 2,
    }

    private readonly Dictionary<ISymbol, Usage> _usage = [];
    private SemanticModel _model = model;
    private bool _watches = false;

    public void Watch(ISymbol symbol)
    {
        Debug.Assert(symbol.DeclaringSyntaxReference!.SyntaxTree == _model.SyntaxTree);

        switch (symbol.Kind)
        {
            case SymbolKind.Parameter:
            case SymbolKind.LocalVariable:
            case SymbolKind.ReturnValue:
                break;

            default:
                throw new ArgumentException(
                    $"Unsupported symbol kind: {symbol.Kind}. Only parameters, return values and local variables are supported.",
                    nameof(symbol));
        }

        if (!_usage.ContainsKey(symbol))
            _usage[symbol] = Usage.Unused;

        _watches = true;
    }

    public void Unwatch(ISymbol symbol)
    {
        _usage.Remove(symbol);
        _watches = _usage.Count > 0;
    }

    public void Clear()
    {
        _usage.Clear();
        _watches = false;
    }

    public void Clear(SemanticModel newModel)
    {
        Clear();
        _model = newModel;
    }

    public Usage GetUsage(ISymbol symbol)
    {
        return _usage[symbol];
    }

    public void VisitIdentifierName(IdentifierNameSyntax node)
    {
        if (!_watches) return;

        if (node.Parent is ReturnValueSyntax)
        {
            // this is the declaration of a return value, not any kind of usage
            return;
        }

        if (!TryGetSymbolUsage(node, out var symbol, out var usage))
        {
            // not a symbol we are watching
            return;
        }

        var newUsage = usage | Usage.Used;
        if (newUsage != usage)
            _usage[symbol] = newUsage;
    }

    public void VisitAssignmentStatement(AssignmentStatementSyntax node)
    {
        if (!_watches) return;

        if (!TryGetSymbolUsage(node.Target, out var symbol, out var usage))
        {
            // not a symbol we are watching
            return;
        }

        if (usage == Usage.Unused)
        {
            _usage[symbol] = Usage.Initialized | Usage.Used;
        }
    }

    public void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (!_watches) return;

        if (node.Expression is not IdentifierNameSyntax ident ||
            !string.Equals(ident.Identifier.ValueText, "Clear") ||
            node.ArgumentList.Arguments.Count != 1 ||
            node.ArgumentList.Arguments[0] is not IdentifierNameSyntax argIdent ||
            !TryGetSymbolUsage(argIdent, out var symbol, out var usage))
        {
            // not a clear, basic flow
            return;
        }

        if (usage == Usage.Unused)
        {
            _usage[symbol] = Usage.Initialized | Usage.Used;
        }
    }

    /// <summary>
    /// Mark manually a symbol as initialized.
    /// </summary>
    public void Initialized(ISymbol symbol)
    {
        if (!_watches) return;

        if (!_usage.TryGetValue(symbol, out var usage))
        {
            // not a symbol we are watching
            return;
        }

        if (usage == Usage.Unused)
        {
            _usage[symbol] = Usage.Initialized | Usage.Used;
        }
    }

    private bool TryGetSymbolUsage(SyntaxNode node, [NotNullWhen(true)] out ISymbol? symbol, out Usage usage)
    {
        usage = Usage.Unused;
        symbol = _model.GetSymbolInfo(node).Symbol;
        return symbol is not null && _usage.TryGetValue(symbol, out usage);
    }
}