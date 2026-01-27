using Kata.Generator.Lowering;
using Kata.Generator.Parsing;
using Kata.Generator.Utilities;

using static Kata.Generator.Emission.Common;

namespace Kata.Generator.Emission;

internal static class ExpandedEmitter
{
    internal static void EmitExpandedBody(LoweredLayout plan, SourceBuilder sb)
    {
    }


    private static void EmitExpandedProperty(LoweredLayout plan, BitFieldModel field, SourceBuilder sb, SourceBuilder readSb, SourceBuilder writeSb)
    {
        int shift            = field.Offset;
        string maskLiteral   = GetMaskLiteral(field.Length);
        string typeName      = field.Type.ToDisplayString();
        string accessibility = GetAccessibility(field.Accessibility);
        bool hasSetter       = field.AccessorKind != AccessorKind.GetOnly;

        if (hasSetter)
        {
            sb.OpenBlock($"{accessibility} partial {typeName} {field.Name}");
            sb.Line("get => field;");
            sb.Line($"set => field = ({typeName})(value & {maskLiteral});");
        }
    }


    private static void EmitReadMethod(LoweredLayout plan, SourceBuilder sb, SourceBuilder readSb)
    {
        string t = plan.Numeric.ToString().ToLowerInvariant();

        sb.OpenBlock($"internal {t} Read()");
        sb.Line($"{t} value = 0;");
        sb.Append(readSb);
        sb.Line("return value;");
        sb.CloseBlock();
        sb.Line();
    }

    private static void EmitWriteMethod(LoweredLayout plan, SourceBuilder sb, SourceBuilder writeSb)
    {
        string t = plan.Numeric.ToString().ToLowerInvariant();

        sb.OpenBlock($"internal void Write({t} value)");
        sb.Append(writeSb);
        sb.CloseBlock();
        sb.Line();
    }
}