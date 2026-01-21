using Microsoft.CodeAnalysis;

namespace Kata.Generator.Utilities;

internal static class TypeHelpers
{
    internal static int GetTypeBitWidth(ITypeSymbol type) => type.SpecialType switch
    {
        SpecialType.System_Boolean => 1,
        SpecialType.System_Byte    => 8,
        SpecialType.System_SByte   => 8,
        SpecialType.System_Int16   => 16,
        SpecialType.System_UInt16  => 16,
        SpecialType.System_Int32   => 32,
        SpecialType.System_UInt32  => 32,
        SpecialType.System_Int64   => 64,
        SpecialType.System_UInt64  => 64,

        _ => type.TypeKind == TypeKind.Enum
            ? GetTypeBitWidth(((INamedTypeSymbol)type).EnumUnderlyingType!)
            : 0
    };

    internal static bool IsSignedType(ITypeSymbol type) => type.SpecialType switch
    {
        SpecialType.System_SByte => true,
        SpecialType.System_Int16 => true,
        SpecialType.System_Int32 => true,
        SpecialType.System_Int64 => true,
        _                        => false
    };
}