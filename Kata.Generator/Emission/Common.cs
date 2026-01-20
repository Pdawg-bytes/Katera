using Microsoft.CodeAnalysis;
using Kata.Generator.Lowering;
using Kata.Generator.Utilities;

namespace Kata.Generator.Emission;

internal static class Common
{
    internal static void EmitOwnedHeader(LoweredLayout plan, SourceBuilder sb)
    {
        var accessibility = plan.Symbol.DeclaredAccessibility switch
        {
            Accessibility.Public   => "public",
            Accessibility.Internal => "internal",
            Accessibility.Private  => "private",
        };

        sb.OpenBlock($"{accessibility} partial struct {plan.Symbol.Name}");
    }
}