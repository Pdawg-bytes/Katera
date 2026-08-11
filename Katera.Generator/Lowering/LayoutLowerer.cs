using System;
using Katera.Generator.Parsing;

namespace Katera.Generator.Lowering;

internal static class LayoutLowerer
{
    internal static LoweredLayout Lower(BitLayoutModel model)
    {
        var size = model.SizeBytes;

        var ownedKind = model.Mode switch
        {
            StorageMode.Expanded => OwnedKind.Expanded,
            StorageMode.Register => OwnedKind.Register,
            StorageMode.Blob     => OwnedKind.Blob,
            StorageMode.Auto     => ResolveAuto(size),
            _                    => throw new Exception("Unreachable")
        };

        NumericKind? numeric = ownedKind switch
        {
            OwnedKind.Expanded => SelectNumeric(size),
            OwnedKind.Register => SelectNumeric(size),
            _                  => null
        };

        return new LoweredLayout
        (
            TypeName:       model.TypeName,
            Namespace:      model.Namespace,
            Accessibility:  model.TypeAccessibility,
            IsRecordStruct: model.IsRecordStruct,
            OwnedKind:      ownedKind,
            SizeBytes:      size,
            BitOrder:       model.BitOrder,
            Fields:         [..model.Items.OfType<BitFieldItem>()],
            Numeric:        numeric
        );
    }

    private static OwnedKind ResolveAuto(int sizeBytes)
        => sizeBytes <= 8
            ? OwnedKind.Register
            : OwnedKind.Blob;

    private static NumericKind SelectNumeric(int sizeBytes)
        => sizeBytes == 1 ? NumericKind.Byte
         : sizeBytes <= 2 ? NumericKind.UShort
         : sizeBytes <= 4 ? NumericKind.UInt
         : NumericKind.ULong;
}