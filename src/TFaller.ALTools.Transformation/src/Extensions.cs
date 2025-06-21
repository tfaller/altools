using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Dynamics.Nav.CodeAnalysis.Syntax;

namespace TFaller.ALTools.Transformation;

public static class Extensions
{
    public static LinkedList<T> ToLinkedList<T>(this IEnumerable<T> source)
    {
        return new LinkedList<T>(source);
    }

    public static bool EqualsOrdinalIgnoreCase(this string a, string? b)
    {
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static readonly ConstructorInfo _separatedSyntaxListConstructor =
        typeof(SeparatedSyntaxList<ParameterSyntax>).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, [typeof(SyntaxNodeOrTokenList)])
        ?? throw new InvalidOperationException("Could not find constructor for SeparatedSyntaxList<ParameterSyntax>.");

    public static ParameterListSyntax AddParameters(this ParameterListSyntax parameterList, SyntaxToken separator, params ParameterSyntax[] parameters)
    {
        var list = parameterList.Parameters.GetWithSeparators();

        if (list.Count > 0)
        {
            list = list.AddRange([separator, .. parameters]);
        }
        else
        {
            list = list.AddRange([.. parameters]);
        }

        return parameterList.WithParameters(
            (SeparatedSyntaxList<ParameterSyntax>)_separatedSyntaxListConstructor.Invoke([list])
        );
    }
}