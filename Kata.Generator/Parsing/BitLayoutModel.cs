using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace Kata.Generator.Parsing;

internal sealed class BitLayoutModel(
    INamedTypeSymbol symbol,
    int sizeBytes,
    StorageMode mode,
    bool allowOverlap,
    Endianness endianness)
{
    internal INamedTypeSymbol Symbol  { get; } = symbol;
    internal int SizeBytes            { get; } = sizeBytes;
    internal StorageMode Mode         { get; } = mode;
    internal bool AllowOverlap        { get; } = allowOverlap;
    internal Endianness Endianness    { get; } = endianness;

    internal List<LayoutItem> Items { get; } = [];

    // Set during validation
    public int ComputedSizeBytes { get; internal set; }
}