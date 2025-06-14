using System;
using System.Collections.Generic;

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
}