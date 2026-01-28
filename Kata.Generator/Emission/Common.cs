using Microsoft.CodeAnalysis;
using Kata.Generator.Parsing;
using Kata.Generator.Lowering;
using Kata.Generator.Utilities;

namespace Kata.Generator.Emission;

internal static class Common
{
    internal static void EmitOwnedHeader(LoweredLayout plan, SourceBuilder sb) =>
        sb.OpenBlock($"{GetAccessibility(plan.Accessibility)} partial struct {plan.TypeName}");


    internal static string GetMaskLiteral(int bitWidth)
    {
        if (bitWidth == 0)
            return "0";

        if (bitWidth >= 64)
            return "0xFFFFFFFFFFFFFFFFUL";

        ulong mask = (1UL << bitWidth) - 1UL;
        return "0x" + mask.ToString("X");
    }

    internal static string GetMaskLiteralULong(int bitWidth) =>
        GetMaskLiteral(bitWidth) + ((bitWidth >= 64) ? "": "UL");

    internal static string GetAccessibility(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Public   => "public",
        Accessibility.Internal => "internal",
        _                      => "private"
    };

    internal static string BitOrderToEndianness(BitOrder bitOrder) => bitOrder switch
    {
        BitOrder.LSBFirst => "LittleEndian",
        BitOrder.MSBFirst => "BigEndian",
        _                 => ""
    };

    internal static string GetSignedIntermediateType(string backingType)
    {
        return backingType switch
        {
            "byte" or "sbyte" or "ushort" or "short" or "uint" or "int" => "int",
            "ulong" or "long"                                           => "long",
            _                                                           => "int"
        };
    }

    internal static int ComputeShift(LoweredLayout plan, BitFieldItem field)
    {
        int totalBits = plan.SizeBytes * 8;
        return plan.BitOrder == BitOrder.LSBFirst
            ? field.Offset
            : totalBits - field.Offset - field.Length;
    }
}