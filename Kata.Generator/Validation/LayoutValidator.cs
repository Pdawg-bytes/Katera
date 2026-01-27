using Kata.Generator.Parsing;
using System.Collections.Immutable;

namespace Kata.Generator.Validation;

internal static class LayoutValidator
{
    internal static ValidationResult Validate(BitLayoutModel model)
    {
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        
        int logicalSizeBits = model.ComputedSizeBytes * 8;
        bool allowOverlap   = model.AllowOverlap && model.Mode != StorageMode.Expanded;

        var spans  = ImmutableArray.CreateBuilder<(BitFieldItem? field, int start, int end)>();
        int cursor = 0;

        foreach (var item in model.Items)
        {
            switch (item)
            {
                case BitFieldItem f:
                    {
                        int start = f.Offset >= 0 ? f.Offset : cursor;
                        int end = start + f.Length;

                        spans.Add((f, start, end));

                        if (!allowOverlap)
                            cursor = end;

                        break;
                    }

                case PadItem pad:
                    {
                        int start = cursor;
                        int end = start + pad.Bits;

                        spans.Add((null, start, end));

                        if (!allowOverlap)
                            cursor = end;

                        break;
                    }
            }
        }


        // BIT006
        int expectedEnd = 0;
        foreach (var (field, start, end) in spans)
        {
            if (start > expectedEnd)
            {
                diagnostics.Add(new DiagnosticInfo(
                    Diagnostics.Bit006_Gap,
                    null,
                    expectedEnd,
                    start));
            }

            expectedEnd = end;
        }

        // BIT001
        if (!allowOverlap)
        {
            foreach (var (field, _, end) in spans)
            {
                if (field is null) continue;
                if (end > logicalSizeBits)
                {
                    diagnostics.Add(new DiagnosticInfo(
                        Diagnostics.Bit001_ExceedsSize,
                        null,
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
            diagnostics.Add(new DiagnosticInfo(
                Diagnostics.Bit003_InvalidSize,
                null,
                model.ComputedSizeBytes));
        }

        // BIT002
        foreach (var (field, _, _) in spans)
        {
            if (field is null) continue;
            int typeBits = field.BackingWidth;
            if (field.Length > typeBits)
            {
                diagnostics.Add(new DiagnosticInfo(
                    Diagnostics.Bit002_TypeTooSmall,
                    null,
                    field.TypeDisplayName,
                    field.Length,
                    typeBits));
            }
        }

        // BIT005
        if (!allowOverlap)
        {
            var owner = new BitFieldItem?[logicalSizeBits];

            foreach (var (field, start, end) in spans)
            {
                if (field is null) continue;
                for (int bit = start; bit < end && bit < logicalSizeBits; bit++)
                {
                    if (owner[bit] is { } other)
                    {
                        diagnostics.Add(new DiagnosticInfo(
                            Diagnostics.Bit005_Overlap,
                            null,
                            field.Name,
                            other.Name,
                            bit));
                    }

                    owner[bit] = field;
                }
            }
        }

        return diagnostics.Count > 0
            ? ValidationResult.Failure(diagnostics.ToArray())
            : ValidationResult.Success();
    }
}