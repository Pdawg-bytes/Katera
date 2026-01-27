using Kata.Generator.Parsing;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Kata.Generator.Lowering;

internal enum OwnedKind
{
    Register,
    Blob,
    Expanded
}

internal enum NumericKind
{
    Byte   = 8,
    UShort = 16,
    UInt   = 32,
    ULong  = 64
}

internal sealed class LoweredLayout(INamedTypeSymbol symbol, OwnedKind ownedKind, int sizeBytes, BitOrder bitOrder, ImmutableList<BitFieldModel> fields, NumericKind? numeric)
{
    internal readonly INamedTypeSymbol Symbol             = symbol;
    internal readonly OwnedKind OwnedKind                 = ownedKind;
    internal readonly int SizeBytes                       = sizeBytes;
    internal readonly BitOrder BitOrder                   = bitOrder;
    internal readonly ImmutableList<BitFieldModel> Fields = fields;
    internal readonly NumericKind? Numeric                = numeric;
}