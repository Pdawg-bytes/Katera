using Katera.Generator.Parsing;
using Katera.Generator.Lowering;
using Katera.Generator.Utilities;

using static Katera.Generator.Emission.Common;

namespace Katera.Generator.Emission;

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
        int shift            = ComputeShift(plan, field);
        string maskLiteral   = GetMaskLiteral(field.Length);
        string typeName      = field.TypeDisplayName;
        string accessibility = GetAccessibility(field.Accessor.Accessibility);
        bool hasSetter       = field.Accessor.AccessorKind != AccessorKind.GetOnly;
        string backingType   = plan.Numeric!.ToString().ToLowerInvariant();

        if (hasSetter)
        {
            string accessor = field.Accessor.AccessorKind == AccessorKind.GetSet ? "set" : "init";

            sb.OpenBlock($"{accessibility} partial {typeName} {field.Name}");
            
            if (field.IsSigned && field.BackingWidth > field.Length)
                EmitSignExtendingGetter(sb, field, maskLiteral, backingType);
            else
                sb.Line("get => field;");

            EmitMaskingSetter(sb, field, maskLiteral, backingType, accessor);
            sb.CloseBlock();
            sb.Line();

            EmitReadLogic(plan, field, shift, readSb, field.Name);
            EmitWriteLogic(plan, field, shift, writeSb, maskLiteral);
        }
        else
        {
            string backingFieldName = $"_{field.Name}";
            sb.Line($"private {typeName} {backingFieldName};");

            sb.OpenBlock($"{accessibility} partial {typeName} {field.Name}");

            if (field.IsSigned && field.BackingWidth > field.Length)
                EmitSignExtendingGetter(sb, field, maskLiteral, backingType, backingFieldName);
            else
                sb.Line($"get => {backingFieldName};");

            sb.CloseBlock();
            sb.Line();

            EmitReadLogic(plan, field, shift, readSb, backingFieldName);
            EmitWriteLogic(plan, field, shift, writeSb, maskLiteral, backingFieldName);
        }
    }


    private static void EmitSignExtendingGetter(SourceBuilder sb, BitFieldItem field, string maskLiteral, string backingType, string backingFieldName = "field")
    {
        string signedIntermediate = GetSignedIntermediateType(backingType);
        int shiftAmount           = GetSignExtensionShiftAmount(signedIntermediate, field.Length);

        sb.OpenBlock("get");
        sb.Line($"{backingType} raw = ({backingType})((({backingType}){backingFieldName}) & ({backingType}){maskLiteral});");
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
        string maskLiteral = GetMaskLiteral(field.Length);

        if (field.TypeDisplayName == "bool")
            readSb.Line($"value |= ({backingType})(({backingFieldName} ? 1 : 0) << {shift});");
        else
            readSb.Line($"value |= ({backingType})((((({backingType}){backingFieldName}) & ({backingType}){maskLiteral}) << {shift}));");
    }

    private static void EmitWriteLogic(LoweredLayout plan, BitFieldItem field, int shift, SourceBuilder writeSb, string maskLiteral, string? backingFieldName = null)
    {
        string backingType  = plan.Numeric!.ToString().ToLowerInvariant();
        string propertyName = backingFieldName ?? field.Name;

        if (field.TypeDisplayName == "bool")
        {
            writeSb.Line($"{propertyName} = ((value >> {shift}) & 1) != 0;");
            return;
        }

        if (field.IsSigned && field.BackingWidth > field.Length)
        {
            string signedIntermediate = GetSignedIntermediateType(backingType);
            int shiftAmount           = GetSignExtensionShiftAmount(signedIntermediate, field.Length);
            string rawVarName         = $"__raw_{propertyName}";

            writeSb.Line($"{backingType} {rawVarName} = ({backingType})((value >> {shift}) & ({backingType}){maskLiteral});");
            writeSb.Line($"{propertyName} = ({field.TypeDisplayName})(({signedIntermediate})({rawVarName} << {shiftAmount}) >> {shiftAmount});");
            return;
        }

        writeSb.Line($"{propertyName} = ({field.TypeDisplayName})((value >> {shift}) & ({backingType}){maskLiteral});");
    }

    private static int GetSignExtensionShiftAmount(string signedIntermediate, int fieldLength)
    {
        int intermediateWidth = signedIntermediate == "long" ? 64 : 32;
        return intermediateWidth - fieldLength;
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