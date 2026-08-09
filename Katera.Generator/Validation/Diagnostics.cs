using Microsoft.CodeAnalysis;
using System.Collections.Immutable;

namespace Katera.Generator.Validation;

internal record ValidationResult(bool IsValid, ImmutableArray<DiagnosticInfo> Diagnostics)
{
    public static ValidationResult Success() => new(true, []);

    public static ValidationResult Failure(params DiagnosticInfo[] diagnostics)
        => new(false, diagnostics.ToImmutableArray());
}

internal record DiagnosticInfo(DiagnosticDescriptor Descriptor, string? LocationString, params object[] MessageArgs);

internal static class Diagnostics
{
    public static readonly DiagnosticDescriptor Kata001_ExceedsSize = new
    (
        id:                 "KATERA001",
        title:              "Fields exceed declared Size",
        messageFormat:      "Field '{0}' ends at bit {1}, which exceeds declared Size ({2} bits)",
        category:           "Katera.BitLayout",
        defaultSeverity:    DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor Kata002_TypeTooSmall = new
    (
        id:                 "KATERA002",
        title:              "Property type too small",
        messageFormat:      "Property type '{0}' cannot hold {1} bits (max {2})",
        category:           "Katera.BitLayout",
        defaultSeverity:    DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor Kata003_InvalidSize = new
    (
        id:                 "KATERA003",
        title:              "Invalid Size",
        messageFormat:      "Size {0} is not supported for this StorageMode",
        category:           "Katera.BitLayout",
        defaultSeverity:    DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor Kata004_InvalidTarget = new
    (
        id:                 "KATERA004",
        title:              "Invalid BitField target",
        messageFormat:      "BitField cannot be applied to '{0}': {1}",
        category:           "Katera.BitLayout",
        defaultSeverity:    DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor Kata005_Overlap = new
    (
        id:                 "KATERA005",
        title:              "Fields overlap",
        messageFormat:      "Field '{0}' overlaps with '{1}' at bit {2}, enable 'AllowOverlap' or fix Offsets",
        category:           "Katera.BitLayout",
        defaultSeverity:    DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor Kata006_Gap = new
    (
        id:                 "KATERA006",
        title:              "Implicit gap",
        messageFormat:      "Gap detected between bit {0} and {1}, use [Pad] to be explicit",
        category:           "Katera.BitLayout",
        defaultSeverity:    DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor Kata007_InvalidLength = new
    (
        id:                 "KATERA007",
        title:              "Invalid BitField length",
        messageFormat:      "BitField length must be {0} (got {1})",
        category:           "Katera.BitLayout",
        defaultSeverity:    DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}