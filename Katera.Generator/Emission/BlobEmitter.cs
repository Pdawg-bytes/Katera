using Katera.Generator.Parsing;
using Katera.Generator.Lowering;
using Katera.Generator.Utilities;

using static Katera.Generator.Emission.Common;

namespace Katera.Generator.Emission;

internal static class BlobEmitter
{
    internal static void EmitBlobBody(LoweredLayout layout, SourceBuilder sb)
    {
        int ulongCount = (layout.SizeBytes + 7) / 8;
        
        for (int i = 0; i < ulongCount; i++)
            sb.Line($"private ulong _data{i};");
        
        sb.Line();

        foreach (var field in layout.Fields)
            EmitBlobProperty(layout, field, sb);

        sb.Line();

        string methodAccessibility = GetAccessibility(layout.Accessibility);

        EmitFrom(layout, methodAccessibility, ulongCount, sb);
        sb.Line();
        EmitTryFrom(layout, methodAccessibility, ulongCount, sb);

        sb.Line();
        EmitWriteTo(layout, methodAccessibility, ulongCount, sb);
    }


    private static void EmitBlobProperty(LoweredLayout layout, BitFieldItem field, SourceBuilder sb)
    {
        int shift            = ComputeShift(layout, field);
        string maskLiteral   = GetMaskLiteralULong(field.Length);
        string accessibility = GetAccessibility(field.Accessor.Accessibility);
        string typeName      = field.TypeDisplayName;

        int ulongIndex       = shift / 64;
        int bitOffsetInULong = shift % 64;

        bool straddlesBoundary = (bitOffsetInULong + field.Length) > 64;

        sb.OpenBlock($"{accessibility} partial {typeName} {field.Name}");

        if (straddlesBoundary)
            EmitStraddlingGetter(sb, field, ulongIndex, bitOffsetInULong, maskLiteral);
        else
            FieldAccessEmitter.EmitFieldGetterInULong(sb, field, $"_data{ulongIndex}", bitOffsetInULong, maskLiteral);

        if (field.Accessor.AccessorKind != AccessorKind.GetOnly)
        {
            string accessor = field.Accessor.AccessorKind == AccessorKind.GetSet ? "set" : "init";
            
            if (straddlesBoundary)
                EmitStraddlingSetter(sb, field, ulongIndex, bitOffsetInULong, maskLiteral, accessor);
            else
                FieldAccessEmitter.EmitFieldSetterInULong(sb, field, $"_data{ulongIndex}", bitOffsetInULong, maskLiteral, accessor);
        }

        sb.CloseBlock();
        sb.Line();
    }

    private static void EmitStraddlingGetter(SourceBuilder sb, BitFieldItem field, int ulongIndex, int bitOffsetInULong, string maskLiteral)
    {
        int bitsInFirstULong  = 64 - bitOffsetInULong;
        int bitsInSecondULong = field.Length - bitsInFirstULong;

        string maskLow  = GetMaskLiteralULong(bitsInFirstULong);
        string maskHigh = GetMaskLiteralULong(bitsInSecondULong);

        if (field.TypeDisplayName == "bool")
        {
            sb.Line($"get => ((_data{ulongIndex} >> {bitOffsetInULong}) & 1) != 0;");
            return;
        }

        if (!field.IsSigned || field.BackingWidth == field.Length)
        {
            sb.OpenBlock("get");
            sb.Line($"ulong low = (_data{ulongIndex} >> {bitOffsetInULong}) & {maskLow};");
            sb.Line($"ulong high = (_data{ulongIndex + 1} & {maskHigh}) << {bitsInFirstULong};");
            sb.Line($"return ({field.TypeDisplayName})(low | high);");
            sb.CloseBlock();
        }
        else
        {
            string signedIntermediate = GetSignedIntermediateType("ulong");
            int shiftAmount           = 64 - field.Length;

            sb.OpenBlock("get");
            sb.Line($"ulong low = (_data{ulongIndex} >> {bitOffsetInULong}) & {maskLow};");
            sb.Line($"ulong high = (_data{ulongIndex + 1} & {maskHigh}) << {bitsInFirstULong};");
            sb.Line($"ulong combined = low | high;");
            sb.Line($"return ({field.TypeDisplayName})(({signedIntermediate})(combined << {shiftAmount}) >> {shiftAmount});");
            sb.CloseBlock();
        }
    }

    private static void EmitStraddlingSetter(SourceBuilder sb, BitFieldItem field, int ulongIndex, int bitOffsetInULong, string maskLiteral, string accessor)
    {
        int bitsInFirstULong = 64 - bitOffsetInULong;
        int bitsInSecondULong = field.Length - bitsInFirstULong;

        string maskLow = GetMaskLiteralULong(bitsInFirstULong);
        string maskHigh = GetMaskLiteralULong(bitsInSecondULong);

        sb.OpenBlock(accessor);
        
        if (field.TypeDisplayName == "bool")
        {
            sb.Line($"_data{ulongIndex} = (_data{ulongIndex} & ~((ulong)1 << {bitOffsetInULong})) | (value ? ((ulong)1 << {bitOffsetInULong}) : 0);");
        }
        else
        {
            sb.Line($"ulong val = (ulong)value & {maskLiteral};");
            sb.Line($"_data{ulongIndex} = (_data{ulongIndex} & ~(({maskLow}) << {bitOffsetInULong})) | ((val & {maskLow}) << {bitOffsetInULong});");
            sb.Line($"_data{ulongIndex + 1} = (_data{ulongIndex + 1} & ~({maskHigh})) | ((val >> {bitsInFirstULong}) & {maskHigh});");
        }
        
        sb.CloseBlock();
    }


    private static void EmitFrom(LoweredLayout layout, string accessibility, int ulongCount, SourceBuilder sb)
    {
        sb.OpenBlock($"{accessibility} static {layout.TypeName} From(global::System.ReadOnlySpan<byte> span)");
        sb.Write($"if (span.Length < {layout.SizeBytes})\r\n", indent: true);
        sb.Write("    throw new global::System.ArgumentException(\"Span too small.\", nameof(span));\r\n", indent: true);
        sb.Line();

        sb.OpenBlock($"return new {layout.TypeName}");
        EmitBlobDataInitializers(layout, ulongCount, sb);
        
        sb.CloseBlock(";");
        sb.CloseBlock();
    }

    private static void EmitTryFrom(LoweredLayout layout, string accessibility, int ulongCount, SourceBuilder sb)
    {
        sb.OpenBlock($"{accessibility} static bool TryFrom(global::System.ReadOnlySpan<byte> span, out {layout.TypeName} value)");

        sb.OpenBlock($"if (span.Length < {layout.SizeBytes})");
        sb.Line("value = default;");
        sb.Line("return false;");
        sb.CloseBlock();
        sb.Line();

        sb.OpenBlock($"value = new {layout.TypeName}");
        EmitBlobDataInitializers(layout, ulongCount, sb);
        
        sb.CloseBlock(";");
        sb.Line();
        sb.Line("return true;");
        sb.CloseBlock();
    }

    private static void EmitBlobDataInitializers(LoweredLayout layout, int ulongCount, SourceBuilder sb)
    {
        for (int i = 0; i < ulongCount; i++)
        {
            int offset = i * 8;
            sb.Write($"_data{i} = {BuildReadUInt64Expression(layout, "span", offset)}", indent: true);

            if (i < ulongCount - 1)
                sb.Write(",\r\n", indent: false);
            else
                sb.Write("\r\n", indent: false);
        }
    }


    private static void EmitWriteTo(LoweredLayout layout, string accessibility, int ulongCount, SourceBuilder sb)
    {
        sb.OpenBlock($"{accessibility} void WriteTo(global::System.Span<byte> span)");
        sb.Write($"if (span.Length < {layout.SizeBytes})\r\n", indent: true);
        sb.Write("    throw new global::System.ArgumentException(\"Span too small.\", nameof(span));\r\n", indent: true);
        sb.Line();

        for (int i = 0; i < ulongCount; i++)
        {
            int offset = i * 8;
            EmitWriteUInt64FromVariable(sb, layout, "span", offset, $"_data{i}");
        }

        sb.CloseBlock();
    }
}