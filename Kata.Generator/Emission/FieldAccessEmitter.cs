using Kata.Generator.Parsing;
using Kata.Generator.Utilities;

using static Kata.Generator.Emission.Common;

namespace Kata.Generator.Emission;

internal static class FieldAccessEmitter
{
    internal static void EmitFieldGetterInULong(SourceBuilder sb, BitFieldItem field, string sourceVar, int shift, string maskLiteral)
    {
        if (field.TypeDisplayName == "bool")
        {
            sb.Line($"get => (({sourceVar} >> {shift}) & 1) != 0;");
            return;
        }

        bool isFullWidth = shift == 0 && field.Length == 64;
        
        if (isFullWidth)
        {
            sb.Line($"get => ({field.TypeDisplayName}){sourceVar};");
            return;
        }

        if (!field.IsSigned || field.BackingWidth == field.Length)
        {
            sb.Line($"get => ({field.TypeDisplayName})((({sourceVar} >> {shift}) & ({maskLiteral})));");
            return;
        }

        string signedIntermediate = GetSignedIntermediateType("ulong");
        int shiftAmount = 64 - field.Length;
        
        sb.OpenBlock("get");
        sb.Line($"ulong raw = ({sourceVar} >> {shift}) & ({maskLiteral});");
        sb.Line($"return ({field.TypeDisplayName})(({signedIntermediate})(raw << {shiftAmount}) >> {shiftAmount});");
        sb.CloseBlock();
    }

    internal static void EmitFieldSetterInULong(SourceBuilder sb, BitFieldItem field, string targetVar, int shift, string maskLiteral, string accessor)
    {
        if (field.TypeDisplayName == "bool")
        {
            sb.Line($"{accessor} => {targetVar} = ({targetVar} & ~((ulong)1 << {shift})) | (value ? ((ulong)1 << {shift}) : 0);");
            return;
        }

        bool isFullWidth = shift == 0 && field.Length == 64;
        
        if (isFullWidth)
        {
            sb.Line($"{accessor} => {targetVar} = (ulong)value;");
            return;
        }

        sb.Line($"{accessor} => {targetVar} = ({targetVar} & ~(({maskLiteral}) << {shift})) | (((((ulong)value) & {maskLiteral}) << {shift}));");
    }
}