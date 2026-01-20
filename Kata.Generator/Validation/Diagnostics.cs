using Microsoft.CodeAnalysis;

namespace Kata.Generator.Validation;

internal static class Diagnostics
{
    public static readonly DiagnosticDescriptor Bit001_ExceedsSize = new
    (
        id: "BIT001",
        title: "Fields exceed declared Size",
        messageFormat: "Field '{0}' ends at bit {1}, which exceeds declared Size ({2} bits)",
        category: "Kata.BitLayout",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor Bit002_TypeTooSmall = new
    (
        id: "BIT002",
        title: "Property type too small",
        messageFormat: "Property type '{0}' cannot hold {1} bits (max {2})",
        category: "Kata.BitLayout",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor Bit003_InvalidSize = new
    (
        id: "BIT003",
        title: "Invalid Size",
        messageFormat: "Size {0} is not supported for this StorageMode",
        category: "Kata.BitLayout",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor Bit004_InvalidTarget = new
    (
        id: "BIT004",
        title: "Invalid BitField target",
        messageFormat: "BitField cannot be applied to '{0}': {1}",
        category: "Kata.BitLayout",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor Bit005_Overlap = new
    (
        id: "BIT005",
        title: "Fields overlap",
        messageFormat: "Field '{0}' overlaps with '{1}' at bit {2}, enable 'AllowOverlap' or fix Offsets",
        category: "Kata.BitLayout",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor Bit006_Gap = new
    (
        id: "BIT006",
        title: "Implicit gap",
        messageFormat: "Gap detected between bit {0} and {1}, use [Pad] to be explicit",
        category: "Kata.BitLayout",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor Bit007_InvalidLength = new
    (
        id: "BIT007",
        title: "Invalid BitField length",
        messageFormat: "BitField length must be greater than zero (got {0})",
        category: "Kata.BitLayout",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}