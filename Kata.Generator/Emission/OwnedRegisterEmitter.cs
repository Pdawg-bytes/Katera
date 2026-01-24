using System;
using Microsoft.CodeAnalysis;
using Kata.Generator.Parsing;
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

        sb.Line();

        string methodAccessibility = plan.Symbol.DeclaredAccessibility == Accessibility.Public
            ? "public"
            : "internal";

        EmitFrom(plan, methodAccessibility, sb);
        sb.Line();
        EmitTryFrom(plan, methodAccessibility, sb);

        sb.Line();
        EmitWriteTo(plan, methodAccessibility, sb);
        sb.Line();
        EmitWriteLogical(plan, methodAccessibility, sb);

        sb.Line();
        sb.Line();
        EmitCasts(plan, backingType, sb);
    }


    private static void EmitRegisterProperty(LoweredLayout plan, BitFieldModel field, string backingType, SourceBuilder sb)
    {
        int totalBits = plan.SizeBytes * 8;

        int shift = plan.Endianness == Endianness.LittleEndian
            ? field.Offset
            : totalBits - field.Offset - field.Length;

        string maskLiteral = GetMaskLiteral(field.Length);
        string accessibility = field.Accessibility switch
        {
            Accessibility.Public   => "public",
            Accessibility.Internal => "internal",
            _                      => "private"
        };

        string typeName = field.Type.ToDisplayString();

        var getter = SelectGetter(plan, field, backingType, shift, maskLiteral);
        var setter = SelectSetter(plan, field, backingType, shift, maskLiteral);

        sb.OpenBlock($"{accessibility} partial {typeName} {field.Name}");
        getter(sb);
        setter?.Invoke(sb);
        sb.CloseBlock();
        sb.Line();
    }


    private static Action<SourceBuilder> SelectGetter(LoweredLayout plan, BitFieldModel field, string backingType, int shift, string maskLiteral)
    {
        if (field.Type.SpecialType == SpecialType.System_Boolean)
            return sb => EmitBoolGetter(sb, shift);

        if (IsFastPathEligible(field, plan.Endianness))
            return sb => EmitFastPathGetter(sb, field, backingType);

        bool isFullWidth = field.Offset == 0 && field.Length == plan.SizeBytes * 8;

        if (isFullWidth)
            return sb => sb.Line($"get => ({field.Type.ToDisplayString()})_value;");

        if (!field.IsSigned || field.BackingWidth == field.Length)
        {
            string typeName = field.Type.ToDisplayString();
            return sb => sb.Line(
                $"get => ({typeName})((({backingType})((_value >> {shift}) & (({backingType}){maskLiteral}))));");
        }

        return sb => EmitSignedGetter(sb, field, backingType, shift, maskLiteral);
    }

    private static Action<SourceBuilder>? SelectSetter(LoweredLayout plan, BitFieldModel field, string backingType, int shift, string maskLiteral)
    {
        if (field.AccessorKind == AccessorKind.GetOnly)
            return null;

        if (field.Type.SpecialType == SpecialType.System_Boolean)
            return sb => EmitBoolSetter(sb, backingType, shift, field.AccessorKind);

        if (IsFastPathEligible(field, plan.Endianness))
            return sb => EmitFastPathSetter(sb, field, backingType);

        bool isFullWidth = field.Offset == 0 && field.Length == plan.SizeBytes * 8;

        if (isFullWidth)
            return sb => sb.Line($"{Accessor(field)} => _value = ({backingType})value;");

        return sb => sb.Line(
            $"{Accessor(field)} => _value = " +
            $"(_value & ~((({backingType}){maskLiteral}) << {shift})) | " +
            $"((((({backingType})value) & ({backingType}){maskLiteral}) << {shift}));");
    }

    private static string Accessor(BitFieldModel field) => field.AccessorKind == AccessorKind.GetInit ? "init" : "set";


    private static void EmitBoolGetter(SourceBuilder sb, int shift)
    {
        sb.Line($"get => ((_value >> {shift}) & 1) != 0;");
    }

    private static void EmitSignedGetter(SourceBuilder sb, BitFieldModel field, string backingType, int shift, string maskLiteral)
    {
        string typeName = field.Type.ToDisplayString();
        string signedIntermediate = GetSignedIntermediateType(backingType);

        sb.OpenBlock("get");
        sb.Line($"{backingType} raw = (_value >> {shift}) & (({backingType}){maskLiteral});");
        sb.Line($"{signedIntermediate} bits = ({signedIntermediate})raw;");
        sb.Line($"{signedIntermediate} sign = ({signedIntermediate})(1 << {field.Length - 1});");
        sb.Line($"return ({typeName})((bits ^ sign) - sign);");
        sb.CloseBlock();
    }

    private static void EmitFastPathGetter(SourceBuilder sb, BitFieldModel field, string backingType)
    {
        string typeName = field.Type.ToDisplayString();
        int index = field.Offset / field.Length;

        sb.OpenBlock("get");
        sb.Line($"ref {typeName} p = ref Unsafe.As<{backingType}, {typeName}>(ref _value);");
        sb.Line($"return Unsafe.Add(ref p, {index});");
        sb.CloseBlock();
    }


    private static void EmitBoolSetter(
        SourceBuilder sb,
        string backingType,
        int shift,
        AccessorKind kind)
    {
        string accessor = kind == AccessorKind.GetInit ? "init" : "set";
        sb.Line(
            $"{accessor} => _value = (_value & ~(({backingType})1 << {shift})) | " +
            $"(value ? (({backingType})1 << {shift}) : 0);");
    }

    private static void EmitFastPathSetter(SourceBuilder sb, BitFieldModel field, string backingType)
    {
        string typeName = field.Type.ToDisplayString();
        int index       = field.Offset / field.Length;
        string accessor = field.AccessorKind == AccessorKind.GetInit ? "init" : "set";

        sb.OpenBlock(accessor);
        sb.Line($"ref {typeName} p = ref Unsafe.As<{backingType}, {typeName}>(ref _value);");
        sb.Line($"Unsafe.Add(ref p, {index}) = value;");
        sb.CloseBlock();
    }


    private static string GetMaskLiteral(int bitWidth)
    {
        if (bitWidth == 0)
            return "0";

        if (bitWidth >= 64)
            return "0xFFFFFFFFFFFFFFFF";

        ulong mask = (1UL << bitWidth) - 1UL;
        return "0x" + mask.ToString("X");
    }

    private static string GetSignedIntermediateType(string backingType)
    {
        return backingType switch
        {
            "byte" or "sbyte" or "ushort" or "short" or "uint" or "int" => "int",
            "ulong" or "long" => "long",
            _ => "int"
        };
    }

    private static bool IsFastPathEligible(BitFieldModel field, Endianness endianness)
    {
        if (endianness != Endianness.LittleEndian)
            return false;

        int width = field.Length;

        if ((width is not (8 or 16 or 32 or 64)) ||
            (field.Offset % width != 0))
            return false;

        return field.BackingWidth >= width;
    }


    private static void EmitFrom(LoweredLayout plan, string accessibility, SourceBuilder sb)
    {
        sb.OpenBlock($"{accessibility} static {plan.Symbol.Name} From(ReadOnlySpan<byte> span)");

        sb.Write($"if (span.Length < {plan.SizeBytes})\n", indent: true);
        sb.Write("    throw new System.ArgumentException(\"Span too small.\", nameof(span));\n", indent: true);
        sb.Line();

        sb.OpenBlock($"return new {plan.Symbol.Name}");
        EmitValueAssignment(plan, sb);
        sb.CloseBlock(";");

        sb.CloseBlock();
    }

    private static void EmitTryFrom(LoweredLayout plan, string accessibility, SourceBuilder sb)
    {
        sb.OpenBlock($"{accessibility} static bool TryFrom(ReadOnlySpan<byte> span, out {plan.Symbol.Name} value)");

        sb.OpenBlock($"if (span.Length < {plan.SizeBytes})");
        sb.Line("value = default;");
        sb.Line("return false;");
        sb.CloseBlock();
        sb.Line();

        sb.OpenBlock($"value = new {plan.Symbol.Name}");
        EmitValueAssignment(plan, sb);
        sb.CloseBlock(";");
        sb.Line();

        sb.Line("return true;");
        sb.CloseBlock();
    }

    private static void EmitValueAssignment(LoweredLayout plan, SourceBuilder sb)
    {
        string backingType = $"UInt{(int)plan.Numeric!}";

        if (plan.Numeric == NumericKind.Byte)
            sb.Write($"_value = span[0]", indent: true);
        else
            sb.Write($"_value = BinaryPrimitives.Read{backingType}{plan.Endianness}(span)", indent: true);

        sb.Line();
    }

    private static void EmitWriteTo(LoweredLayout plan, string accessibility, SourceBuilder sb)
    {
        int numericBits = (int)plan.Numeric!;

        sb.OpenBlock($"{accessibility} void WriteTo(Span<byte> span)");

        sb.Write($"if (span.Length < {numericBits / 8})\n", indent: true);
        sb.Write("    throw new System.ArgumentException(\"Span too small.\", nameof(span));\n", indent: true);
        sb.Line();

        string backingType = $"UInt{numericBits}";

        if (plan.Numeric == NumericKind.Byte)
            sb.Line("span[0] = _value;");
        else
            sb.Line($"BinaryPrimitives.Write{backingType}{plan.Endianness}(span, _value);");

        sb.CloseBlock();
    }

    private static void EmitWriteLogical(LoweredLayout plan, string accessibility, SourceBuilder sb)
    {
        int numericBits = (int)plan.Numeric!;

        sb.OpenBlock($"{accessibility} void WriteLogical(Span<byte> span)");

        sb.Write($"if (span.Length < {plan.SizeBytes})\n", indent: true);
        sb.Write("    throw new System.ArgumentException(\"Span too small.\", nameof(span));\n", indent: true);
        sb.Line();

        string endianness = plan.Endianness switch
        {
            Endianness.LittleEndian => "LittleEndian",
            Endianness.BigEndian => "BigEndian"
        };

        string backingType = $"UInt{numericBits}";

        sb.Line($"Span<byte> tmp = stackalloc byte[{numericBits / 8}];");

        if (plan.Numeric == NumericKind.Byte)
            sb.Line("tmp[0] = _value;");
        else
            sb.Line($"BinaryPrimitives.Write{backingType}{endianness}(tmp, _value);");

        sb.Line($"tmp.Slice(0, {plan.SizeBytes}).CopyTo(span);");

        sb.CloseBlock();
    }

    private static void EmitCasts(LoweredLayout plan, string backingType, SourceBuilder sb)
    {
        string symbolName = plan.Symbol.Name;
        sb.Line($"public static implicit operator {backingType}({symbolName} value) => value._value;");
        sb.Line();
        sb.Line($"public static explicit operator {symbolName}({backingType} value) => new {symbolName} {{ _value = value }};");
    }
}