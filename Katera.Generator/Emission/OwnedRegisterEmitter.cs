using System;
using Katera.Generator.Parsing;
using Katera.Generator.Lowering;
using Katera.Generator.Utilities;

using static Katera.Generator.Emission.Common;

namespace Katera.Generator.Emission;

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

        string methodAccessibility = GetAccessibility(plan.Accessibility);

        EmitFrom(plan, methodAccessibility, sb);
        sb.Line();
        EmitTryFrom(plan, methodAccessibility, sb);

        sb.Line();
        EmitWriteTo(plan, methodAccessibility, sb);

        sb.Line();
        sb.Line();
        EmitConversions(plan, methodAccessibility, backingType, sb);
    }


    private static void EmitRegisterProperty(LoweredLayout plan, BitFieldItem field, string backingType, SourceBuilder sb)
    {
        int shift = ComputeShift(plan, field);

        string maskLiteral   = backingType == "ulong"
            ? GetMaskLiteralULong(field.Length)
            : GetMaskLiteral(field.Length);
        string accessibility = GetAccessibility(field.Accessor.Accessibility);

        string typeName = field.TypeDisplayName;

        var getter = SelectGetter(plan, field, backingType, shift, maskLiteral);
        var setter = SelectSetter(plan, field, backingType, shift, maskLiteral);

        sb.OpenBlock($"{accessibility} partial {typeName} {field.Name}");
        getter(sb);
        setter?.Invoke(sb);
        sb.CloseBlock();
        sb.Line();
    }


    private static Action<SourceBuilder> SelectGetter(LoweredLayout plan, BitFieldItem field, string backingType, int shift, string maskLiteral)
    {
        int totalBits = (int)plan.Numeric!;

        if (backingType == "ulong" && !IsFastPathEligible(field, shift, totalBits))
            return sb => FieldAccessEmitter.EmitFieldGetterInULong(sb, field, "_value", shift, maskLiteral);

        if (field.TypeDisplayName == "bool")
            return sb => sb.Line($"get => ((_value >> {shift}) & 1) != 0;");

        if (IsFastPathEligible(field, shift, totalBits))
            return sb => EmitFastPathGetter(sb, field, backingType, shift, totalBits);

        bool isFullWidth = field.Offset == 0 && field.Length == (int)plan.Numeric! * 8;

        if (isFullWidth)
            return sb => sb.Line($"get => ({field.TypeDisplayName})_value;");

        if (!field.IsSigned || field.BackingWidth == field.Length)
        {
            string typeName = field.TypeDisplayName;
            return sb => sb.Line(
                $"get => ({typeName})((({backingType})((_value >> {shift}) & (({backingType}){maskLiteral}))));");
        }

        return sb => EmitSignedGetter(sb, field, (int)plan.Numeric!, backingType, shift, maskLiteral);
    }

    private static Action<SourceBuilder>? SelectSetter(LoweredLayout plan, BitFieldItem field, string backingType, int shift, string maskLiteral)
    {
        if (field.Accessor.AccessorKind == AccessorKind.GetOnly)
            return null;

        int totalBits = (int)plan.Numeric!;
        string accessor = field.Accessor.AccessorKind == AccessorKind.GetSet ? "set" : "init";

        if (backingType == "ulong" && !IsFastPathEligible(field, shift, totalBits))
            return sb => FieldAccessEmitter.EmitFieldSetterInULong(sb, field, "_value", shift, maskLiteral, accessor);

        if (field.TypeDisplayName == "bool")
            return sb => EmitBoolSetter(sb, accessor, backingType, shift);

        if (IsFastPathEligible(field, shift, totalBits))
            return sb => EmitFastPathSetter(sb, field, accessor, backingType, shift, totalBits);

        bool isFullWidth = field.Offset == 0 && field.Length == (int)plan.Numeric! * 8;

        if (isFullWidth)
            return sb => sb.Line($"{accessor} => _value = ({backingType})value;");

        return sb =>
        {
            sb.OpenBlock(accessor);
            sb.Line($"{backingType} masked = ({backingType})((({backingType})value) & ({backingType}){maskLiteral});");
            sb.Line($"_value = ({backingType})((_value & ~((({backingType}){maskLiteral}) << {shift})) | (masked << {shift}));");
            sb.CloseBlock();
        };
    }


    private static void EmitSignedGetter(SourceBuilder sb, BitFieldItem field, int backingWidth, string backingType, int shift, string maskLiteral)
    {
        string typeName           = field.TypeDisplayName;
        string signedIntermediate = GetSignedIntermediateType(backingType);
        int shiftAmount           = signedIntermediate == "long" ? 64 - field.Length : 32 - field.Length;

        sb.OpenBlock("get");
        sb.Line($"{backingType} raw = ({backingType})((_value >> {shift}) & (({backingType}){maskLiteral}));");
        sb.Line($"return ({typeName})(({signedIntermediate})(raw << {shiftAmount}) >> {shiftAmount});");
        sb.CloseBlock();
    }

    private static void EmitFastPathGetter(SourceBuilder sb, BitFieldItem field, string backingType, int shift, int totalBits)
    {
        string typeName = field.TypeDisplayName;
        var (littleIndex, bigIndex) = ComputeFastPathIndices(shift, field.Length, totalBits);

        sb.OpenBlock("get");
        sb.Line($"Span<{backingType}> s = MemoryMarshal.CreateSpan(ref _value, 1);");
        sb.Line($"ref {typeName} p = ref Unsafe.As<{backingType}, {typeName}>(ref MemoryMarshal.GetReference(s));");

        if (littleIndex == bigIndex)
            sb.Line($"return Unsafe.Add(ref p, {littleIndex});");
        else
            sb.Line($"return Unsafe.Add(ref p, BitConverter.IsLittleEndian ? {littleIndex} : {bigIndex});");

        sb.CloseBlock();
    }


    private static void EmitBoolSetter(SourceBuilder sb, string accessor, string backingType, int shift) =>
        sb.Line(
            $"{accessor} => _value = ({backingType})((_value & ~(({backingType})1 << {shift})) | " +
            $"(value ? (({backingType})1 << {shift}) : ({backingType})0));");

    private static void EmitFastPathSetter(SourceBuilder sb, BitFieldItem field, string accessor, string backingType, int shift, int totalBits)
    {
        string typeName = field.TypeDisplayName;
        var (littleIndex, bigIndex) = ComputeFastPathIndices(shift, field.Length, totalBits);

        sb.OpenBlock(accessor);
        sb.Line($"Span<{backingType}> s = MemoryMarshal.CreateSpan(ref _value, 1);");
        sb.Line($"ref {typeName} p = ref Unsafe.As<{backingType}, {typeName}>(ref MemoryMarshal.GetReference(s));");

        if (littleIndex == bigIndex)
            sb.Line($"Unsafe.Add(ref p, {littleIndex}) = value;");
        else
            sb.Line($"Unsafe.Add(ref p, BitConverter.IsLittleEndian ? {littleIndex} : {bigIndex}) = value;");

        sb.CloseBlock();
    }


    private static bool IsFastPathEligible(BitFieldItem field, int shift, int totalBits)
    {
        int width = field.Length;

        if (field.TypeDisplayName == "bool")
            return false;

        if (field.BackingWidth != width)
            return false;

        if (width is not (8 or 16 or 32 or 64))
            return false;

        if (shift < 0 || shift + width > totalBits)
            return false;

        return shift % width == 0;
    }

    private static (int LittleEndianIndex, int BigEndianIndex) ComputeFastPathIndices(int shift, int laneWidth, int totalBits)
    {
        int laneCount = totalBits / laneWidth;
        int little    = shift     / laneWidth;
        int big       = laneCount - 1 - little;

        return (little, big);
    }


    private static void EmitFrom(LoweredLayout plan, string accessibility, SourceBuilder sb)
    {
        sb.OpenBlock($"{accessibility} static {plan.TypeName} From(ReadOnlySpan<byte> span)");

        sb.Write($"if (span.Length < {plan.SizeBytes})\r\n", indent: true);
        sb.Write("    throw new System.ArgumentException(\"Span too small.\", nameof(span));\r\n", indent: true);
        sb.Line();

        sb.OpenBlock($"return new {plan.TypeName}");
        EmitValueAssignment(plan, sb);
        sb.CloseBlock(";");

        sb.CloseBlock();
    }

    private static void EmitTryFrom(LoweredLayout plan, string accessibility, SourceBuilder sb)
    {
        sb.OpenBlock($"{accessibility} static bool TryFrom(ReadOnlySpan<byte> span, out {plan.TypeName} value)");

        sb.OpenBlock($"if (span.Length < {plan.SizeBytes})");
        sb.Line("value = default;");
        sb.Line("return false;");
        sb.CloseBlock();
        sb.Line();

        sb.OpenBlock($"value = new {plan.TypeName}");
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

        sb.Write($"if (span.Length < {plan.SizeBytes})\r\n", indent: true);
        sb.Write("    throw new System.ArgumentException(\"Span too small.\", nameof(span));\r\n", indent: true);
        sb.Line();

        if (plan.Numeric == NumericKind.Byte)
            sb.Line("span[0] = _value;");
        else
            sb.Line($"BinaryPrimitives.WriteUInt{numericBits}{BitOrderToEndianness(plan.BitOrder)}(span, _value);");

        sb.CloseBlock();
    }

    private static void EmitConversions(LoweredLayout plan, string accessibility, string backingType, SourceBuilder sb)
    {
        string symbolName = plan.TypeName;
        sb.Line($"{accessibility} void SetRaw({backingType} value) => _value = value;");
        sb.Line();
        sb.Line($"public static implicit operator {backingType}({symbolName} value) => value._value;");
        sb.Line();
        sb.Line($"public static explicit operator {symbolName}({backingType} value) => new {symbolName} {{ _value = value }};");
    }
}