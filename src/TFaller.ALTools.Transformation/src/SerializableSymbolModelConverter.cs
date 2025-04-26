using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.SymbolReference;
using System;
using System.Linq;
using System.Reflection;

namespace TFaller.ALTools.Transformation;

public class SerializableSymbolModelConverter
{
    private static readonly Lazy<MethodInfo> _converter = new(LoadConvertModuleToSerializableSymbolModel);

    private static MethodInfo LoadConvertModuleToSerializableSymbolModel()
    {
        var navAssembly = AppDomain.CurrentDomain
            .GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "Microsoft.Dynamics.Nav.CodeAnalysis")
            ?? throw new InvalidOperationException("Could not find Microsoft.Dynamics.Nav.CodeAnalysis assembly.");

        var converter = navAssembly.GetType("Microsoft.Dynamics.Nav.CodeAnalysis.SymbolReference.SerializableSymbolModelConverter")
            ?? throw new InvalidOperationException("Could not find SerializableSymbolModelConverter type.");

        return converter.GetMethod("ConvertModuleToSerializableSymbolModel", BindingFlags.NonPublic | BindingFlags.Static, [typeof(Compilation)])
            ?? throw new InvalidOperationException("Could not find ConvertModuleToSerializableSymbolModel method.");
    }

    public static ModuleDefinition ConvertModuleToSerializableSymbolModel(Compilation compilation)
    {
        return (ModuleDefinition)_converter.Value.Invoke(null, [compilation])!;
    }
}