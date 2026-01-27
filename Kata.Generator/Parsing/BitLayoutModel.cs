using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Kata.Generator.Parsing;

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
    int ComputedSizeBytes
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

internal enum AccessorKind
{
    GetOnly,
    GetSet,
    GetInit
}