using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Kata.Generator.Validation;

internal record ValidationResult(bool IsValid, ImmutableArray<DiagnosticInfo> Diagnostics)
{
    public static ValidationResult Success() => new(true, []);
    
    public static ValidationResult Failure(params DiagnosticInfo[] diagnostics) 
        => new(false, diagnostics.ToImmutableArray());
}

internal record DiagnosticInfo(DiagnosticDescriptor Descriptor, string? LocationString, params object[] MessageArgs);