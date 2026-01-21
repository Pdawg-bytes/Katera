using System.Linq;
using Kata.Generator.Parsing;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;

using static Kata.Generator.Utilities.TypeHelpers;

namespace Kata.Generator.Validation;

internal static class LayoutValidator
{
    internal static void Validate(BitLayoutModel model, SourceProductionContext ctx)
    {
        int logicalSizeBits = model.ComputedSizeBytes * 8;
        bool allowOverlap   = model.AllowOverlap && model.Mode != StorageMode.Expanded;

        var spans  = new List<(BitFieldModel field, int start, int end)>();
        int cursor = 0;

        foreach (var item in model.Items)
        {
            switch (item)
            {
                case BitFieldModel f:
                    {
                        int start = f.Offset >= 0 ? f.Offset : cursor;
                        int end = start + f.Length;

                        spans.Add((f, start, end));

                        if (!allowOverlap)
                            cursor = end;

                        break;
                    }

                case PadModel pad:
                    {
                        if (!allowOverlap)
                            cursor += pad.Bits;
                        break;
                    }
            }
        }


        // BIT006
        int expectedEnd = 0;
        foreach (var (field, start, _) in spans)
        {
            if (start > expectedEnd)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.Bit006_Gap,
                    model.Symbol.Locations.FirstOrDefault(),
                    expectedEnd,
                    start));
            }

            expectedEnd = start + field.Length;
        }

        // BIT001
        if (!allowOverlap)
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
        if ((model.Mode == StorageMode.Expanded  || 
             model.Mode == StorageMode.Register) &&
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


        if (!allowOverlap)
        {
            var owner = new BitFieldModel?[logicalSizeBits];

            foreach (var (field, start, end) in spans)
            {
                for (int bit = start; bit < end && bit < logicalSizeBits; bit++)
                {
                    if (owner[bit] is { } other)
                    {
                        ctx.ReportDiagnostic(Diagnostic.Create(
                            Diagnostics.Bit005_Overlap,
                            field.Location,
                            field.Name,
                            other.Name,
                            bit));
                    }

                    owner[bit] = field;
                }
            }
        }
    }
}