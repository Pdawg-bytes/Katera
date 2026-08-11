using Microsoft.CodeAnalysis;
using Katera.Generator.Parsing;
using Katera.Generator.Lowering;
using Katera.Generator.Utilities;
using System.Text;

namespace Katera.Generator.Emission;

internal static class Common
{
    internal static void EmitOwnedHeader(LoweredLayout plan, SourceBuilder sb) =>
        sb.OpenBlock($"{GetAccessibility(plan.Accessibility)} partial {GetOwnedTypeKeyword(plan)} {plan.TypeName}");

    private static string GetOwnedTypeKeyword(LoweredLayout plan)
        => plan.IsRecordStruct ? "record struct" : "struct";

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
        Accessibility.Public               => "public",
        Accessibility.Internal             => "internal",
        Accessibility.Private              => "private",
        Accessibility.Protected            => "protected",
        Accessibility.ProtectedOrInternal  => "protected internal",
        Accessibility.ProtectedAndInternal => "private protected",
        _                                  => "private"
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

    internal static string BuildReadUInt64Expression(LoweredLayout plan, string spanExpr, int offset)
    {
        int remainingBytes = plan.SizeBytes - offset;

        if (remainingBytes >= 8)
            return $"BinaryPrimitives.ReadUInt64{BitOrderToEndianness(plan.BitOrder)}({spanExpr}.Slice({offset}))";

        var expression = new StringBuilder();

        for (int b = 0; b < remainingBytes; b++)
        {
            if (b > 0)
                expression.Append(" | ");

            if (plan.BitOrder == BitOrder.LSBFirst)
                expression.Append($"((ulong){spanExpr}[{offset + b}] << {b * 8})");
            else
                expression.Append($"((ulong){spanExpr}[{offset + b}] << {(remainingBytes - 1 - b) * 8})");
        }

        return expression.Length == 0 ? "0" : expression.ToString();
    }

    internal static void EmitReadUInt64IntoVariable(SourceBuilder sb, LoweredLayout plan, string spanExpr, int offset, string variableName)
        => sb.Line($"ulong {variableName} = {BuildReadUInt64Expression(plan, spanExpr, offset)};");

    internal static void EmitWriteUInt64FromVariable(SourceBuilder sb, LoweredLayout plan, string spanExpr, int offset, string valueExpr)
    {
        int remainingBytes = plan.SizeBytes - offset;

        if (remainingBytes >= 8)
        {
            sb.Line($"BinaryPrimitives.WriteUInt64{BitOrderToEndianness(plan.BitOrder)}({spanExpr}.Slice({offset}), {valueExpr});");
            return;
        }

        for (int b = 0; b < remainingBytes; b++)
        {
            if (plan.BitOrder == BitOrder.LSBFirst)
                sb.Line($"{spanExpr}[{offset + b}] = (byte)({valueExpr} >> {b * 8});");
            else
                sb.Line($"{spanExpr}[{offset + b}] = (byte)({valueExpr} >> {(remainingBytes - 1 - b) * 8});");
        }
    }
}