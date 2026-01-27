using Kata.Generator.Validation;
using System.Collections.Immutable;

namespace Kata.Generator.Parsing;

internal record ParseResult<T>(T? Value, ImmutableArray<DiagnosticInfo> Diagnostics)
{
    public bool IsSuccess => Value is not null && Diagnostics.IsEmpty;
    
    public static ParseResult<T> Success(T value) => new(value, []);
    
    public static ParseResult<T> Failure(params DiagnosticInfo[] diagnostics) 
        => new(default, diagnostics.ToImmutableArray());
}