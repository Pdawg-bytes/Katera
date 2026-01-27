using Kata.Generator.Parsing;
using Kata.Generator.Lowering;
using Kata.Generator.Utilities;

using static Kata.Generator.Emission.Common;

namespace Kata.Generator.Emission;

internal static class ExpandedEmitter
{
    internal static void EmitExpandedBody(LoweredLayout plan, SourceBuilder sb)
    {
        SourceBuilder readSb  = new();
        SourceBuilder writeSb = new();
        readSb.SetIndent(3);
        writeSb.SetIndent(3);

        foreach (var field in plan.Fields)
            EmitExpandedProperty(plan, field, sb, readSb, writeSb);

        sb.Line();

        EmitReadMethod(plan, sb, readSb);
        EmitWriteMethod(plan, sb, writeSb);
    }


    private static void EmitExpandedProperty(LoweredLayout plan, BitFieldItem field, SourceBuilder sb, SourceBuilder readSb, SourceBuilder writeSb)
    {
        int totalBits = plan.SizeBytes * 8;
        int shift     = ComputeShift(plan, field);

        string maskLiteral   = GetMaskLiteral(field.Length);
        string typeName      = field.TypeDisplayName;
        string accessibility = GetAccessibility(field.Accessor.Accessibility);
        bool hasSetter       = field.Accessor.AccessorKind != AccessorKind.GetOnly;
        string accessor      = field.Accessor.AccessorKind == AccessorKind.GetSet ? "set" : "init";
        string backingType   = plan.Numeric!.ToString().ToLowerInvariant();

        if (hasSetter)
        {
            sb.OpenBlock($"{accessibility} partial {typeName} {field.Name}");
            
            if (field.IsSigned && field.BackingWidth > field.Length)
                EmitSignExtendingGetter(sb, field, maskLiteral, totalBits, backingType);
            else
                sb.Line("get => field;");

            EmitMaskingSetter(sb, field, maskLiteral, backingType, accessor);
            sb.CloseBlock();
            sb.Line();

            string backingFieldName = hasSetter ? field.Name : $"_{field.Name}";
            EmitReadLogic(plan, field, shift, readSb, backingFieldName);
            EmitWriteLogic(plan, field, shift, writeSb, maskLiteral);
        }
        else
        {
            string backingFieldName = $"_{field.Name}";
            sb.Line($"private {typeName} {backingFieldName};");

            sb.OpenBlock($"{accessibility} partial {typeName} {field.Name}");

            if (field.IsSigned && field.BackingWidth > field.Length)
                EmitSignExtendingGetter(sb, field, maskLiteral, totalBits, backingType);
            else
                sb.Line($"get => {backingFieldName};");

            sb.CloseBlock();
            sb.Line();

            EmitReadLogic(plan, field, shift, readSb, backingFieldName);
            EmitWriteLogic(plan, field, shift, writeSb, maskLiteral, backingFieldName);
        }
    }


    private static void EmitSignExtendingGetter(SourceBuilder sb, BitFieldItem field, string maskLiteral, int totalBits, string backingType, string backingFieldName = "field")
    {
        string signedIntermediate = GetSignedIntermediateType(backingType);
        int shiftAmount           = totalBits - field.Length;

        sb.OpenBlock("get");
        sb.Line($"{backingType} raw = ({backingType}){backingFieldName} & {maskLiteral};");
        sb.Line($"return ({field.TypeDisplayName})(({signedIntermediate})(raw << {shiftAmount}) >> {shiftAmount});");
        sb.CloseBlock();
    }

    private static void EmitMaskingSetter(SourceBuilder sb, BitFieldItem field, string maskLiteral, string backingType, string accessor)
    {
        if (field.TypeDisplayName == "bool")
            sb.Line($"{accessor} => field = value;");
        else
            sb.Line($"{accessor} => field = ({field.TypeDisplayName})(({backingType})value & {maskLiteral});");
    }


    private static void EmitReadLogic(LoweredLayout plan, BitFieldItem field, int shift, SourceBuilder readSb, string backingFieldName)
    {
        string backingType = plan.Numeric!.ToString().ToLowerInvariant();

        if (field.TypeDisplayName == "bool")
            readSb.Line($"value |= ({backingType})(({backingFieldName} ? 1 : 0) << {shift});");
        else
            readSb.Line($"value |= ({backingType})(({backingType}){backingFieldName} << {shift});");
    }

    private static void EmitWriteLogic(LoweredLayout plan, BitFieldItem field, int shift, SourceBuilder writeSb, string maskLiteral, string? backingFieldName = null)
    {
        string backingType  = plan.Numeric!.ToString().ToLowerInvariant();
        string propertyName = backingFieldName ?? field.Name;

        if (field.TypeDisplayName == "bool")
            writeSb.Line($"{propertyName} = ((value >> {shift}) & 1) != 0;");
        else
            writeSb.Line($"{propertyName} = ({field.TypeDisplayName})((value >> {shift}) & ({backingType}){maskLiteral});");
    }

    private static void EmitReadMethod(LoweredLayout plan, SourceBuilder sb, SourceBuilder readSb)
    {
        string t = plan.Numeric!.ToString().ToLowerInvariant();

        sb.OpenBlock($"internal {t} Read()");
        sb.Line($"{t} value = 0;");
        sb.Append(readSb);
        sb.Line("return value;");
        sb.CloseBlock();
        sb.Line();
    }

    private static void EmitWriteMethod(LoweredLayout plan, SourceBuilder sb, SourceBuilder writeSb)
    {
        string t = plan.Numeric!.ToString().ToLowerInvariant();

        sb.OpenBlock($"internal void Write({t} value)");
        sb.Append(writeSb);
        sb.CloseBlock();
    }
}