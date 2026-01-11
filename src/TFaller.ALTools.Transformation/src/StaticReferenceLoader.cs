using System;
using System.Collections.Generic;
using Microsoft.Dynamics.Nav.CodeAnalysis;
using Microsoft.Dynamics.Nav.CodeAnalysis.Diagnostics;
using Microsoft.Dynamics.Nav.CodeAnalysis.SymbolReference;

namespace TFaller.ALTools.Transformation;

/// <summary>
/// Can be used to load symbols from a already loaded ModuleDefinition
/// </summary>
public class StaticReferenceLoader : ISymbolReferenceLoader
{
    private readonly ISymbolReferenceLoader? _nextLoader;
    private readonly ModuleInfo _moduleInfo;

    public StaticReferenceLoader(ModuleInfo moduleInfo, ISymbolReferenceLoader? nextLoader = null)
    {
        _moduleInfo = moduleInfo;
        _nextLoader = nextLoader;

        if (_moduleInfo.ModuleMetadata == null)
            throw new ArgumentException("ModuleMetadata must be set.", nameof(moduleInfo));
    }

    public StaticReferenceLoader(ModuleDefinition module, ISymbolReferenceLoader? nextLoader = null)
        : this(new ModuleInfo(module), nextLoader)
    {
    }

    public IEnumerable<SymbolReferenceSpecification> GetDependencies(SymbolReferenceSpecification reference, IList<Diagnostic> diagnostics)
    {
        // We have no dependencies
        return [];
    }

    public ModuleDefinition? LoadModule(SymbolReferenceSpecification reference, IList<Diagnostic> diagnostics)
    {
        return LoadModuleInfo(reference, diagnostics)?.ModuleMetadata;
    }

    public ModuleInfo LoadModuleInfo(SymbolReferenceSpecification reference, IList<Diagnostic> diagnostics, LoadModuleInfoFlags loadOptions = LoadModuleInfoFlags.Symbols)
    {
        var module = _moduleInfo.ModuleMetadata!;

        if (module.AppId == reference.AppId)
        {
            return _moduleInfo;
        }

        return _nextLoader?.LoadModuleInfo(reference, diagnostics, loadOptions)!;
    }
}