using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using Katera.Generator.Validation;

namespace Katera.Generator.Parsing;

internal record ParseResult<T>(T? Value, ImmutableArray<DiagnosticInfo> Diagnostics)
{
    public bool IsSuccess => Value is not null && Diagnostics.IsEmpty;

    public static ParseResult<T> Success(T value)                             => new(value, []);
    public static ParseResult<T> Failure(params DiagnosticInfo[] diagnostics) => new(default, [..diagnostics]);
}

internal record BitLayoutModel
(
    string TypeName,
    string Namespace,
    Accessibility TypeAccessibility,
    int SizeBytes,
    StorageMode Mode,
    bool AllowOverlap,
    BitOrder BitOrder,
    ImmutableArray<LayoutItem> Items,
    ImmutableArray<BitFieldStub> BitFieldStubs
);

internal abstract record LayoutItem;


internal record BitFieldItem
(
    string Name,
    string TypeDisplayName,
    int Length,
    int Offset,
    int BackingWidth,
    bool IsSigned,
    AccessorInfo Accessor
) : LayoutItem;

internal record PadItem(int Bits) : LayoutItem;


internal record AccessorInfo
(
    Accessibility Accessibility,
    AccessorKind AccessorKind,
    bool IsRequired
);

internal record BitFieldStub
(
    string Name,
    string TypeDisplayName,
    Accessibility Accessibility,
    bool IsStatic,
    bool HasGetter,
    bool HasSetter,
    bool SetterIsInit,
    Accessibility GetterAccessibility,
    Accessibility SetterAccessibility
);

internal enum AccessorKind
{
    GetOnly,
    GetSet,
    GetInit
}