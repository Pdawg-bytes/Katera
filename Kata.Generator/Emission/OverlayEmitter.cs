using System;
using Kata.Generator.Parsing;
using Kata.Generator.Lowering;
using Kata.Generator.Utilities;

using static Kata.Generator.Emission.Common;

namespace Kata.Generator.Emission;

internal static class OverlayEmitter
{
    internal static void EmitOverlay(LoweredLayout plan, SourceBuilder sb)
    {
        string overlayName   = $"{plan.TypeName}View";
        string accessibility = GetAccessibility(plan.Accessibility);

        sb.OpenBlock($"{accessibility} ref struct {overlayName}(Span<byte> span)");

        sb.Line("private Span<byte> _span = span;");
        sb.Line();

        foreach (var field in plan.Fields)
            EmitOverlayProperty(plan, field, sb);

        sb.Line();
        EmitOverMethods(plan, overlayName, accessibility, sb);
        sb.Line($"{accessibility} readonly Span<byte> AsSpan() => _span;");

        sb.CloseBlock();
    }

    private static void EmitOverlayProperty(LoweredLayout plan, BitFieldItem field, SourceBuilder sb)
    {
        int shift            = ComputeShift(plan, field);
        string maskLiteral   = GetMaskLiteral(field.Length);
        string accessibility = GetAccessibility(field.Accessor.Accessibility);
        string typeName      = field.TypeDisplayName;
        string backingType   = plan.Numeric!.ToString().ToLowerInvariant();

        var getter = SelectOverlayGetter(plan, field, backingType, shift, maskLiteral);
        var setter = SelectOverlaySetter(plan, field, backingType, shift, maskLiteral);

        sb.OpenBlock($"{accessibility} {typeName} {field.Name}");
        getter(sb);
        setter?.Invoke(sb);
        sb.CloseBlock();
        sb.Line();
    }

    private static Action<SourceBuilder> SelectOverlayGetter(LoweredLayout plan, BitFieldItem field, string backingType, int shift, string maskLiteral)
    {
        if (field.TypeDisplayName == "bool")
            return sb => EmitOverlayBoolGetter(sb, plan, shift);

        bool isFullWidth = field.Offset == 0 && field.Length == (int)plan.Numeric! * 8;

        if (isFullWidth)
            return sb => EmitOverlayFullWidthGetter(sb, plan, field);

        if (!field.IsSigned || field.BackingWidth == field.Length)
            return sb => EmitOverlayUnsignedGetter(sb, plan, field, backingType, shift, maskLiteral);

        return sb => EmitOverlaySignedGetter(sb, plan, field, backingType, shift, maskLiteral);
    }

    private static Action<SourceBuilder>? SelectOverlaySetter(LoweredLayout plan, BitFieldItem field, string backingType, int shift, string maskLiteral)
    {
        if (field.Accessor.AccessorKind == AccessorKind.GetOnly)
            return null;

        string accessor = field.Accessor.AccessorKind == AccessorKind.GetSet ? "set" : "init";

        if (field.TypeDisplayName == "bool")
            return sb => EmitOverlayBoolSetter(sb, plan, shift, accessor);

        bool isFullWidth = field.Offset == 0 && field.Length == (int)plan.Numeric! * 8;

        if (isFullWidth)
            return sb => EmitOverlayFullWidthSetter(sb, plan, field, accessor);

        return sb => EmitOverlayGeneralSetter(sb, plan, field, backingType, shift, maskLiteral, accessor);
    }


    private static void EmitReadBacking(SourceBuilder sb, LoweredLayout plan, string varName)
    {
        string numericType = plan.Numeric!.ToString().ToLowerInvariant();

        if (plan.Numeric == NumericKind.Byte)
        {
            sb.Line($"{numericType} {varName} = _span[0];");
            return;
        }

        sb.Line($"{numericType} {varName} = Unsafe.ReadUnaligned<{numericType}>(ref MemoryMarshal.GetReference(_span));");

        if (plan.BitOrder == BitOrder.MSBFirst)
            sb.Line($"if (BitConverter.IsLittleEndian) {varName} = BinaryPrimitives.ReverseEndianness({varName});");
        else
            sb.Line($"if (!BitConverter.IsLittleEndian) {varName} = BinaryPrimitives.ReverseEndianness({varName});");
    }

    private static void EmitOverlayBoolGetter(SourceBuilder sb, LoweredLayout plan, int shift)
    {
        sb.OpenBlock("get");
        EmitReadBacking(sb, plan, "raw");
        sb.Line($"return ((raw >> {shift}) & 1) != 0;");
        sb.CloseBlock();
    }

    private static void EmitOverlayFullWidthGetter(SourceBuilder sb, LoweredLayout plan, BitFieldItem field)
    {
        sb.OpenBlock("get");

        if (plan.Numeric == NumericKind.Byte)
        {
            sb.Line($"return ({field.TypeDisplayName})_span[0];");
        }
        else
        {
            EmitReadBacking(sb, plan, "raw");
            sb.Line($"return ({field.TypeDisplayName})raw;");
        }

        sb.CloseBlock();
    }

    private static void EmitOverlayUnsignedGetter(SourceBuilder sb, LoweredLayout plan, BitFieldItem field, string backingType, int shift, string maskLiteral)
    {
        sb.OpenBlock("get");
        EmitReadBacking(sb, plan, "raw");
        sb.Line($"return ({field.TypeDisplayName})((({backingType})((raw >> {shift}) & (({backingType}){maskLiteral}))));");
        sb.CloseBlock();
    }

    private static void EmitOverlaySignedGetter(SourceBuilder sb, LoweredLayout plan, BitFieldItem field, string backingType, int shift, string maskLiteral)
    {
        string signedIntermediate = GetSignedIntermediateType(backingType);
        int shiftAmount           = (int)plan.Numeric! - field.Length;

        sb.OpenBlock("get");
        EmitReadBacking(sb, plan, "rawValue");
        sb.Line($"{backingType} raw = (rawValue >> {shift}) & (({backingType}){maskLiteral});");
        sb.Line($"return ({field.TypeDisplayName})(({signedIntermediate})(raw << {shiftAmount}) >> {shiftAmount});");
        sb.CloseBlock();
    }


    private static void EmitWriteBacking(SourceBuilder sb, LoweredLayout plan, string varName)
    {
        if (plan.Numeric == NumericKind.Byte)
        {
            sb.Line($"_span[0] = {varName};");
            return;
        }

        if (plan.BitOrder == BitOrder.MSBFirst)
            sb.Line($"if (BitConverter.IsLittleEndian) {varName} = BinaryPrimitives.ReverseEndianness({varName});");
        else
            sb.Line($"if (!BitConverter.IsLittleEndian) {varName} = BinaryPrimitives.ReverseEndianness({varName});");

        sb.Line($"Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(_span), {varName});");
    }

    private static void EmitOverlayBoolSetter(SourceBuilder sb, LoweredLayout plan, int shift, string accessor)
    {
        string backingTypeLower = plan.Numeric!.ToString().ToLowerInvariant();

        sb.OpenBlock(accessor);
        EmitReadBacking(sb, plan, "raw");
        sb.Line($"raw = (raw & ~(({backingTypeLower})1 << {shift})) | (value ? (({backingTypeLower})1 << {shift}) : 0);");
        EmitWriteBacking(sb, plan, "raw");
        sb.CloseBlock();
    }

    private static void EmitOverlayFullWidthSetter(SourceBuilder sb, LoweredLayout plan, BitFieldItem field, string accessor)
    {
        string backingTypeLower = plan.Numeric!.ToString().ToLowerInvariant();

        sb.OpenBlock(accessor);

        if (plan.Numeric == NumericKind.Byte)
        {
            sb.Line($"_span[0] = ({backingTypeLower})value;");
        }
        else
        {
            sb.Line($"{backingTypeLower} raw = ({backingTypeLower})value;");
            EmitWriteBacking(sb, plan, "raw");
        }

        sb.CloseBlock();
    }

    private static void EmitOverlayGeneralSetter(SourceBuilder sb, LoweredLayout plan, BitFieldItem field, string backingType, int shift, string maskLiteral, string accessor)
    {
        sb.OpenBlock(accessor);
        EmitReadBacking(sb, plan, "raw");
        sb.Line($"raw = (raw & ~((({backingType}){maskLiteral}) << {shift})) | ((((({backingType})value) & ({backingType}){maskLiteral}) << {shift}));");
        EmitWriteBacking(sb, plan, "raw");
        sb.CloseBlock();
    }


    private static void EmitOverMethods(LoweredLayout plan, string overlayName, string accessibility, SourceBuilder sb)
    {
        sb.OpenBlock($"{accessibility} static {overlayName} Over(Span<byte> span)");
        sb.Line($"if (span.Length < {plan.SizeBytes})");
        sb.Write("    throw new System.ArgumentException(\"Span too small.\", nameof(span));\r\n", indent: true);
        sb.Line();
        sb.Line($"return new {overlayName}(span.Slice(0, {plan.SizeBytes}));");
        sb.CloseBlock();
        sb.Line();

        sb.OpenBlock($"{accessibility} static {overlayName} Over(ref byte reference)");
        sb.Line($"Span<byte> span = MemoryMarshal.CreateSpan(ref reference, {plan.SizeBytes});");
        sb.Line($"return new {overlayName}(span);");
        sb.CloseBlock();
        sb.Line();
    }
}