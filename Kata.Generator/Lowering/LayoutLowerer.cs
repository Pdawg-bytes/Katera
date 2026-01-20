using System;
using System.Collections.Immutable;
using System.Linq;
using Kata.Generator.Parsing;

namespace Kata.Generator.Lowering;

internal static class LayoutLowerer
{
    public static LoweredLayout Lower(BitLayoutModel model)
    {
        var size = model.ComputedSizeBytes;

        var ownedKind = model.Mode switch
        {
            StorageMode.Expanded => OwnedKind.Expanded,
            StorageMode.Register => OwnedKind.Register,
            StorageMode.Blob => OwnedKind.Blob,
            StorageMode.Auto => ResolveAuto(size),
            _ => throw new Exception("Unreachable")
        };

        NumericKind? numeric = ownedKind switch
        {
            OwnedKind.Expanded => SelectNumeric(size),
            OwnedKind.Register => SelectNumeric(size),
            _ => null
        };

        return new LoweredLayout
        (
            symbol:     model.Symbol,
            ownedKind:  ownedKind,
            sizeBytes:  size,
            endianness: model.Endianness,
            numeric:    numeric,
            fields:     model.Items.OfType<BitFieldModel>().ToImmutableList()
        );
    }

    private static OwnedKind ResolveAuto(int sizeBytes)
        => sizeBytes is 1 or 2 or 4 or 8
            ? OwnedKind.Register
            : OwnedKind.Blob;

    private static NumericKind SelectNumeric(int sizeBytes)
        => sizeBytes <= 2 ? NumericKind.UShort
         : sizeBytes <= 4 ? NumericKind.UInt
         : NumericKind.ULong;
}