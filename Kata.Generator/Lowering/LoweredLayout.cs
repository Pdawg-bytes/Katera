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

internal record LoweredLayout
(
    string TypeName,
    string Namespace,
    Accessibility Accessibility,
    OwnedKind OwnedKind,
    int SizeBytes,
    BitOrder BitOrder,
    ImmutableArray<BitFieldItem> Fields,
    NumericKind? Numeric
);