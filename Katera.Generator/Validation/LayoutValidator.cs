using Katera.Generator.Parsing;
using System.Collections.Immutable;

namespace Katera.Generator.Validation;

internal static class LayoutValidator
{
    private readonly record struct LayoutSpan(BitFieldItem? Field, int Start, int End);

    internal static ValidationResult Validate(BitLayoutModel model)
    {
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        
        int logicalSizeBits = model.SizeBytes * 8;
        bool allowOverlap   = model.AllowOverlap && model.Mode != StorageMode.Expanded;

        var spans = BuildSpans(model, allowOverlap);

        // KATA006
        int expectedEnd = 0;
        foreach (var span in spans)
        {
            int start = span.Start;
            int end   = span.End;

            if (start > expectedEnd)
            {
                diagnostics.Add(new DiagnosticInfo(
                    Diagnostics.Kata006_Gap,
                    null,
                    expectedEnd,
                    start
                ));
            }

            expectedEnd = end;
        }

        // KATA001
        if (!allowOverlap)
        {
            foreach (var span in spans)
            {
                var field = span.Field;
                int end   = span.End;

                if (field is null) continue;
                if (end > logicalSizeBits)
                {
                    diagnostics.Add(new DiagnosticInfo(
                        Diagnostics.Kata001_ExceedsSize,
                        null,
                        field.Name,
                        end,
                        logicalSizeBits
                    ));
                }
            }
        }

        // KATA003
        if (HasInvalidStorageSize(model))
        {
            diagnostics.Add(new DiagnosticInfo(
                Diagnostics.Kata003_InvalidSize,
                null,
                model.SizeBytes
            ));
        }

        // KATA002
        foreach (var span in spans)
        {
            var field = span.Field;
            if (field is null) continue;

            int typeBits = field.BackingWidth;
            if (field.Length > typeBits)
            {
                diagnostics.Add(new DiagnosticInfo(
                    Diagnostics.Kata002_TypeTooSmall,
                    null,
                    field.TypeDisplayName,
                    field.Length,
                    typeBits
                ));
            }
        }

        // KATA005
        if (!allowOverlap)
        {
            var owner = new BitFieldItem?[logicalSizeBits];

            foreach (var span in spans)
            {
                var field = span.Field;
                int start = span.Start;
                int end   = span.End;

                if (field is null) continue;

                for (int bit = start; bit < end && bit < logicalSizeBits; bit++)
                {
                    if (owner[bit] is { } other)
                    {
                        diagnostics.Add(new DiagnosticInfo(
                            Diagnostics.Kata005_Overlap,
                            null,
                            field.Name,
                            other.Name,
                            bit
                        ));
                    }

                    owner[bit] = field;
                }
            }
        }

        return diagnostics.Count > 0
            ? ValidationResult.Failure(diagnostics.ToArray())
            : ValidationResult.Success();
    }


    private static ImmutableArray<LayoutSpan> BuildSpans(BitLayoutModel model, bool allowOverlap)
    {
        var spansBuilder = ImmutableArray.CreateBuilder<LayoutSpan>();
        int cursor       = 0;

        foreach (var item in model.Items)
        {
            switch (item)
            {
                case BitFieldItem field:
                {
                    int start = field.Offset >= 0 ? field.Offset : cursor;
                    int end   = start + field.Length;

                    spansBuilder.Add(new LayoutSpan(field, start, end));

                    if (!allowOverlap)
                        cursor = end;

                    break;
                }

                case PadItem pad:
                {
                    int start = cursor;
                    int end   = start + pad.Bits;

                    spansBuilder.Add(new LayoutSpan(null, start, end));

                    if (!allowOverlap)
                        cursor = end;

                    break;
                }
            }
        }

        return spansBuilder.ToImmutable();
    }

    private static bool HasInvalidStorageSize(BitLayoutModel model)
    {
        if (model.Mode is StorageMode.Expanded or StorageMode.Register)
            return model.SizeBytes > 8;

        if (model.Mode == StorageMode.Blob)
            return model.SizeBytes <= 8;

        return false;
    }
}