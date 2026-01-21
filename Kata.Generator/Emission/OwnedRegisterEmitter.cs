using Kata.Generator.Parsing;
using Microsoft.CodeAnalysis;
using Kata.Generator.Lowering;
using Kata.Generator.Utilities;

using static Kata.Generator.Utilities.TypeHelpers;

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

        bool isFullWidth = field.Offset == 0 && field.Length == totalBits;
        bool isBool      = field.Type.SpecialType == SpecialType.System_Boolean;
        bool isSigned    = field.IsSigned;

        string maskLiteral = GetMaskLiteral(field.Length);
        string accessibility = field.Accessibility switch
        {
            Accessibility.Public   => "public",
            Accessibility.Internal => "internal",
            _                      => "private"
        };

        string typeName = field.Type.ToDisplayString();

        sb.OpenBlock($"{accessibility} partial {typeName} {field.Name}");

        if (isBool)
        {
            sb.Line($"get => ((_value >> {shift}) & 1) != 0;");

            if (field.AccessorKind != AccessorKind.GetOnly)
            {
                string accessor = field.AccessorKind == AccessorKind.GetInit ? "init" : "set";
                sb.Line($"{accessor} => _value = (_value & ~(({backingType})1 << {shift})) | (value ? (({backingType})1 << {shift}) : 0);");
            }

            sb.CloseBlock();
            sb.Line();
            return;
        }

        if (isFullWidth)
            sb.Line($"get => ({typeName})_value;");
        else if (!isSigned || GetTypeBitWidth(field.Type) == field.Length)
            sb.Line($"get => ({typeName})(({backingType})((_value >> {shift}) & (({backingType}){maskLiteral})));");
        else
        {
            string signedIntermediate = GetSignedIntermediateType(backingType);

            sb.OpenBlock("get");
            sb.Line($"{backingType} raw = (_value >> {shift}) & (({backingType}){maskLiteral});");
            sb.Line($"{signedIntermediate} bits = ({signedIntermediate})raw;");
            sb.Line($"{signedIntermediate} sign = ({signedIntermediate})(1 << {field.Length - 1});");
            sb.Line($"return ({typeName})((bits ^ sign) - sign);");
            sb.CloseBlock();
        }

        if (field.AccessorKind != AccessorKind.GetOnly)
        {
            string accessor = field.AccessorKind == AccessorKind.GetInit ? "init" : "set";

            if (isFullWidth)
                sb.Line($"{accessor} => _value = ({backingType})value;");
            else
                sb.Line($"{accessor} => _value = (_value & ~((({backingType}){maskLiteral}) << {shift})) | (((({backingType})value) & ({backingType}){maskLiteral}) << {shift});");
        }

        sb.CloseBlock();
        sb.Line();
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
            "ulong" or "long"                                           => "long",
            _                                                           => "int"
        };
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
        string endianness = plan.Endianness switch
        {
            Endianness.LittleEndian => "LittleEndian",
            Endianness.BigEndian => "BigEndian"
        };

        string backingType = $"UInt{(int)plan.Numeric!}";

        if (plan.Numeric == NumericKind.Byte)
            sb.Write($"_value = span[0]", indent: true);
        else
            sb.Write($"_value = BinaryPrimitives.Read{backingType}{endianness}(span)", indent: true);

        sb.Line();
    }


    private static void EmitWriteTo(LoweredLayout plan, string accessibility, SourceBuilder sb)
    {
        int numericBits = (int)plan.Numeric!;

        sb.OpenBlock($"{accessibility} void WriteTo(Span<byte> span)");

        sb.Write($"if (span.Length < {numericBits / 8})\n", indent: true);
        sb.Write("    throw new System.ArgumentException(\"Span too small.\", nameof(span));\n", indent: true);
        sb.Line();

        string endianness = plan.Endianness switch
        {
            Endianness.LittleEndian => "LittleEndian",
            Endianness.BigEndian    => "BigEndian"
        };

        string backingType = $"UInt{numericBits}";

        if (plan.Numeric == NumericKind.Byte)
            sb.Line("span[0] = _value;");
        else
            sb.Line($"BinaryPrimitives.Write{backingType}{endianness}(span, _value);");

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
            Endianness.BigEndian    => "BigEndian"
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