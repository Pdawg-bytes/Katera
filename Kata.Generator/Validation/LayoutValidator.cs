using System.Linq;
using Kata.Generator.Parsing;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace Kata.Generator.Validation;

internal static class LayoutValidator
{
    internal static void Validate(BitLayoutModel model, SourceProductionContext ctx)
    {
        int cursor = 0;
        var spans = new List<(BitFieldModel field, int start, int end)>();

        foreach (var item in model.Items)
        {
            switch (item)
            {
                case BitFieldModel f:
                    {
                        int start = f.Offset >= 0 ? f.Offset : cursor;

                        if (start > cursor)
                        {
                            ctx.ReportDiagnostic(Diagnostic.Create(
                                Diagnostics.Bit006_Gap,
                                model.Symbol.Locations.FirstOrDefault(),
                                cursor,
                                start));
                        }

                        int end = start + f.Length;
                        spans.Add((f, start, end));
                        cursor = end;
                        break;
                    }

                case PadModel pad:
                    {
                        cursor += pad.Bits;
                        break;
                    }
            }
        }

        int logicalSizeBits = model.ComputedSizeBytes * 8;

        // BIT001
        if (model.SizeBytes > 0)
        {
            foreach (var (field, _, end) in spans)
            {
                if (end > logicalSizeBits)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.Bit001_ExceedsSize,
                        field.Location,
                        field.Name,
                        end,
                        logicalSizeBits));
                }
            }
        }

        // BIT003
        if (model.Mode == StorageMode.Register &&
            model.ComputedSizeBytes is not (1 or 2 or 4 or 8))
        {
            ctx.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.Bit003_InvalidSize,
                model.Symbol.Locations.FirstOrDefault(),
                model.ComputedSizeBytes));
        }

        if (model.Mode == StorageMode.Expanded &&
            model.ComputedSizeBytes > 8)
        {
            ctx.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.Bit003_InvalidSize,
                model.Symbol.Locations.FirstOrDefault(),
                model.ComputedSizeBytes));
        }

        // BIT002
        foreach (var (field, _, _) in spans)
        {
            int typeBits = GetTypeBitWidth(field.Type);
            if (field.Length > typeBits)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.Bit002_TypeTooSmall,
                    field.Location,
                    field.Type.ToDisplayString(),
                    field.Length,
                    typeBits));
            }
        }

        // BIT005
        var owner = new BitFieldModel?[logicalSizeBits];

        foreach (var (field, start, end) in spans)
        {
            for (int bit = start; bit < end && bit < logicalSizeBits; bit++)
            {
                if (owner[bit] is { } other)
                {
                    bool overlapAllowed =
                        model.AllowOverlap &&
                        model.Mode != StorageMode.Expanded;

                    if (!overlapAllowed)
                    {
                        ctx.ReportDiagnostic(Diagnostic.Create(
                            Diagnostics.Bit005_Overlap,
                            field.Location,
                            field.Name,
                            other.Name,
                            bit));
                    }
                }

                owner[bit] = field;
            }
        }
    }


    private static int GetTypeBitWidth(ITypeSymbol type)
    {
        return type.SpecialType switch
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
    }
}