using Kata.Generator.Lowering;
using Kata.Generator.Parsing;
using Kata.Generator.Utilities;
using Microsoft.CodeAnalysis;
using System;
using System.Numerics;
using static Kata.Generator.Emission.Common;

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
        EmitConversions(plan, methodAccessibility, backingType, sb);
    }


    private static void EmitRegisterProperty(LoweredLayout plan, BitFieldModel field, string backingType, SourceBuilder sb)
    {
        int totalBits = plan.SizeBytes * 8;

        int shift = plan.BitOrder == BitOrder.LSBFirst
            ? field.Offset
            : totalBits - field.Offset - field.Length;

        string maskLiteral   = GetMaskLiteral(field.Length);
        string accessibility = GetAccessibility(field.Accessibility);

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
            return sb => sb.Line($"get => ((_value >> {shift}) & 1) != 0;");

        if (IsFastPathEligible(field, plan.BitOrder))
            return sb => EmitFastPathGetter(sb, field, backingType);

        bool isFullWidth = field.Offset == 0 && field.Length == (int)plan.Numeric! * 8;

        if (isFullWidth)
            return sb => sb.Line($"get => ({field.Type.ToDisplayString()})_value;");

        if (!field.IsSigned || field.BackingWidth == field.Length)
        {
            string typeName = field.Type.ToDisplayString();
            return sb => sb.Line(
                $"get => ({typeName})((({backingType})((_value >> {shift}) & (({backingType}){maskLiteral}))));");
        }

        return sb => EmitSignedGetter(sb, field, (int)plan.Numeric!, backingType, shift, maskLiteral);
    }

    private static Action<SourceBuilder>? SelectSetter(LoweredLayout plan, BitFieldModel field, string backingType, int shift, string maskLiteral)
    {
        if (field.AccessorKind == AccessorKind.GetOnly)
            return null;

        string accessor = field.AccessorKind == AccessorKind.GetSet ? "set" : "init";

        if (field.Type.SpecialType == SpecialType.System_Boolean)
            return sb => EmitBoolSetter(sb, accessor, backingType, shift, field.AccessorKind);

        if (IsFastPathEligible(field, plan.BitOrder))
            return sb => EmitFastPathSetter(sb, field, accessor, backingType);

        bool isFullWidth = field.Offset == 0 && field.Length == (int)plan.Numeric! * 8;

        if (isFullWidth)
            return sb => sb.Line($"{accessor} => _value = ({backingType})value;");

        return sb => sb.Line(
            $"{accessor} => _value = " +
            $"(_value & ~((({backingType}){maskLiteral}) << {shift})) | " +
            $"((((({backingType})value) & ({backingType}){maskLiteral}) << {shift}));");
    }


    private static void EmitSignedGetter(SourceBuilder sb, BitFieldModel field, int backingWidth, string backingType, int shift, string maskLiteral)
    {
        string typeName           = field.Type.ToDisplayString();
        string signedIntermediate = GetSignedIntermediateType(backingType);
        int shiftAmount           = backingWidth - field.Length;

        sb.OpenBlock("get");
        sb.Line($"{backingType} raw = (_value >> {shift}) & (({backingType}){maskLiteral});");
        sb.Line($"return ({typeName})(({signedIntermediate})(raw << {shiftAmount}) >> {shiftAmount});");
        sb.CloseBlock();
    }

    private static void EmitFastPathGetter(SourceBuilder sb, BitFieldModel field, string backingType)
    {
        string typeName = field.Type.ToDisplayString();
        int index       = field.Offset / field.Length;

        sb.OpenBlock("get");
        sb.Line($"Span<{backingType}> s = MemoryMarshal.CreateSpan(ref _value, 1);");
        sb.Line($"ref {typeName} p = ref Unsafe.As<{backingType}, {typeName}>(ref MemoryMarshal.GetReference(s));");
        sb.Line($"return Unsafe.Add(ref p, {index});");
        sb.CloseBlock();
    }


    private static void EmitBoolSetter(SourceBuilder sb, string accessor, string backingType, int shift, AccessorKind kind) =>
        sb.Line(
            $"{accessor} => _value = (_value & ~(({backingType})1 << {shift})) | " +
            $"(value ? (({backingType})1 << {shift}) : 0);");

    private static void EmitFastPathSetter(SourceBuilder sb, BitFieldModel field, string accessor, string backingType)
    {
        string typeName = field.Type.ToDisplayString();
        int index       = field.Offset / field.Length;

        sb.OpenBlock(accessor);
        sb.Line($"Span<{backingType}> s = MemoryMarshal.CreateSpan(ref _value, 1);");
        sb.Line($"ref {typeName} p = ref Unsafe.As<{backingType}, {typeName}>(ref MemoryMarshal.GetReference(s));");
        sb.Line($"Unsafe.Add(ref p, {index}) = value;");
        sb.CloseBlock();
    }


    private static string GetSignedIntermediateType(string backingType)
    {
        return backingType switch
        {
            "byte" or "sbyte" or "ushort" or "short" or "uint" or "int" => "int",
            "ulong" or "long"                                           => "long",
            _                                                           => "int"
        };
    }

    private static bool IsFastPathEligible(BitFieldModel field, BitOrder endianness)
    {
        // TODO: determine if we can make this safe across platforms.
        return false;

        if (endianness != BitOrder.LSBFirst)
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

        sb.Write($"if (span.Length < {plan.SizeBytes})\r\n", indent: true);
        sb.Write("    throw new System.ArgumentException(\"Span too small.\", nameof(span));\r\n", indent: true);
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
            sb.Write($"_value = BinaryPrimitives.Read{backingType}{BitOrderToEndianness(plan.BitOrder)}(span)", indent: true);

        sb.Line();
    }

    private static void EmitWriteTo(LoweredLayout plan, string accessibility, SourceBuilder sb)
    {
        int numericBits = (int)plan.Numeric!;

        sb.OpenBlock($"{accessibility} void WriteTo(Span<byte> span)");

        sb.Write($"if (span.Length < {numericBits / 8})\r\n", indent: true);
        sb.Write("    throw new System.ArgumentException(\"Span too small.\", nameof(span));\r\n", indent: true);
        sb.Line();

        if (plan.Numeric == NumericKind.Byte)
            sb.Line("span[0] = _value;");
        else
            sb.Line($"BinaryPrimitives.WriteUInt{numericBits}{BitOrderToEndianness(plan.BitOrder)}(span, _value);");

        sb.CloseBlock();
    }

    private static void EmitWriteLogical(LoweredLayout plan, string accessibility, SourceBuilder sb)
    {
        int numericBits = (int)plan.Numeric!;

        if ((numericBits / 8) == plan.SizeBytes)
        {
            sb.Write($"{accessibility} void WriteLogical(Span<byte> span) => WriteTo(span);\r\n", indent: true);
            return;
        }

        sb.OpenBlock($"{accessibility} void WriteLogical(Span<byte> span)");

        sb.Write($"if (span.Length < {plan.SizeBytes})\r\n", indent: true);
        sb.Write("    throw new System.ArgumentException(\"Span too small.\", nameof(span));\r\n", indent: true);
        sb.Line();

        sb.Line($"Span<byte> tmp = stackalloc byte[{numericBits / 8}];");

        if (plan.Numeric == NumericKind.Byte)
            sb.Line("tmp[0] = _value;");
        else
            sb.Line($"BinaryPrimitives.WriteUInt{numericBits}{BitOrderToEndianness(plan.BitOrder)}(tmp, _value);");

        sb.Line($"tmp.Slice(0, {plan.SizeBytes}).CopyTo(span);");

        sb.CloseBlock();
    }

    private static void EmitConversions(LoweredLayout plan, string accessibility, string backingType, SourceBuilder sb)
    {
        string symbolName = plan.Symbol.Name;
        sb.Line($"{accessibility} void SetRaw({backingType} value) => _value = value;");
        sb.Line();
        sb.Line($"public static implicit operator {backingType}({symbolName} value) => value._value;");
        sb.Line();
        sb.Line($"public static explicit operator {symbolName}({backingType} value) => new {symbolName} {{ _value = value }};");
    }
}