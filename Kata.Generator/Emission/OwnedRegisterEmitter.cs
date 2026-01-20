using Kata.Generator.Parsing;
using Microsoft.CodeAnalysis;
using Kata.Generator.Lowering;
using Kata.Generator.Utilities;

namespace Kata.Generator.Emission;

internal static class OwnedRegisterEmitter
{
    internal static void EmitOwnedRegisterBody(LoweredLayout plan, SourceBuilder sb)
    {
        string backingType = plan.Numeric.ToString().ToLowerInvariant();
        sb.Line($"private {backingType} _value;");
        sb.Line();

        foreach (var field in plan.Fields)
            EmitRegisterProperty(plan, field, backingType, sb);

        string methodAccessibility = plan.Symbol.DeclaredAccessibility == Accessibility.Public
            ? "public"
            : "internal";

        EmitFrom(plan, methodAccessibility, backingType, sb);
    }


    private static void EmitRegisterProperty(LoweredLayout plan, BitFieldModel field, string backingType, SourceBuilder sb)
    {
        int totalBits = plan.SizeBytes * 8;

        int shift = plan.Endianness == Endianness.LittleEndian
            ? field.Offset
            : totalBits - field.Offset - field.Length;

        string mask = field.Length == 64
            ? "ulong.MaxValue"
            : $"((({backingType})1 << {field.Length}) - 1)";

        var accessibility = field.Accessibility switch
        {
            Accessibility.Public   => "public",
            Accessibility.Internal => "internal",
            _ => "private"
        };

        sb.OpenBlock($"{accessibility} partial {field.Type.ToDisplayString()} {field.Name}");

        sb.Line($"get => ({field.Type.ToDisplayString()})((_value >> {shift}) & {mask});");

        if (field.AccessorKind != AccessorKind.GetOnly)
        {
            sb.Write(field.AccessorKind == AccessorKind.GetInit ? "init => " : "set => ", indent: true);
            sb.Write($"_value = (_value & ~({mask} << {shift})) | ((({backingType})value & {mask}) << {shift});", indent: false);
            sb.Line();
        }

        sb.CloseBlock();
        sb.Line();
    }

    private static void EmitFrom(LoweredLayout plan, string accessibility, string backingType, SourceBuilder sb)
    {
        sb.OpenBlock($"{accessibility} static {plan.Symbol.Name} From(ReadOnlySpan<byte> span)");

        sb.Write($"if (span.Length != {plan.SizeBytes})", indent: true);
        sb.Write("    throw new System.ArgumentException(\"Span too small.\", nameof(span));", indent: true);

        // TODO: write _value
    }
}