using System;
using System.Collections.Generic;

namespace TFaller.ALTools.Transformation;

/// <summary>
/// A simple helper to store data of an AL identifier in a dictionary.
/// </summary>
public class IdentifierDictionary<T> : Dictionary<string, T>
{
    public IdentifierDictionary() : base(StringComparer.InvariantCultureIgnoreCase) { }

    public IdentifierDictionary(IDictionary<string, T> dictionary) : base(dictionary, StringComparer.InvariantCultureIgnoreCase) { }

    public void AddRange(IDictionary<string, T> dictionary)
    {
        foreach (var item in dictionary)
        {
            Add(item.Key, item.Value);
        }
    }
}