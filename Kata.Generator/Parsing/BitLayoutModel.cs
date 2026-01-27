using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace Kata.Generator.Parsing;

internal sealed class BitLayoutModel(
    INamedTypeSymbol symbol,
    int sizeBytes,
    StorageMode mode,
    bool allowOverlap,
    BitOrder bitOrder)
{
    internal INamedTypeSymbol Symbol { get; } = symbol;
    internal int SizeBytes           { get; } = sizeBytes;
    internal StorageMode Mode        { get; } = mode;
    internal bool AllowOverlap       { get; } = allowOverlap;
    internal BitOrder BitOrder       { get; } = bitOrder;

    internal List<LayoutItem> Items { get; } = [];

    public int ComputedSizeBytes { get; internal set; }
}