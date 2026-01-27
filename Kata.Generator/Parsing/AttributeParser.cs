using System.Linq;
using Microsoft.CodeAnalysis;
using Kata.Generator.Validation;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Kata.Generator.Utilities.TypeHelpers;

namespace Kata.Generator.Parsing;

internal static class AttributeParser
{
    internal static LayoutParseResult ParseLayout(GeneratorAttributeSyntaxContext ctx)
    {
        var symbol = (INamedTypeSymbol)ctx.TargetSymbol;

        bool isPartial = symbol.DeclaringSyntaxReferences.Any(syntaxRef =>
            syntaxRef.GetSyntax() is TypeDeclarationSyntax declaration &&
            declaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)));

        if (!symbol.IsValueType || !isPartial)
        {
            string reason =
                !symbol.IsValueType ? "target must be a struct" :
                "struct must be declared 'partial'";

            var diag = Diagnostic.Create(
                Diagnostics.Bit004_InvalidTarget,
                symbol.Locations.FirstOrDefault(),
                symbol.ToDisplayString(),
                reason);

            return new LayoutParseResult(null, diag);
        }

        int size          = 0;
        StorageMode mode  = StorageMode.Auto;
        bool allowOverlap = false;
        BitOrder bitOrder = BitOrder.LSBFirst;

        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() != "Kata.BitLayoutAttribute")
                continue;

            foreach (var arg in attr.NamedArguments)
            {
                switch (arg.Key)
                {
                    case "Size":         size = (int)arg.Value.Value!; break;
                    case "Mode":         mode = (StorageMode)arg.Value.Value!; break;
                    case "AllowOverlap": allowOverlap = (bool)arg.Value.Value!; break;
                    case "BitOrder":     bitOrder = (BitOrder)arg.Value.Value!; break;
                }
            }
        }

        var model = new BitLayoutModel(symbol, size, mode, allowOverlap, bitOrder);
        return new LayoutParseResult(model, null);
    }


    internal static void ParseFields(BitLayoutModel model, SourceProductionContext ctx)
    {
        foreach (var member in model.Symbol.GetMembers())
        {
            foreach (var attr in member.GetAttributes())
            {
                if (attr.AttributeClass?.ToDisplayString() != "Kata.PadAttribute")
                    continue;

                if (member.IsStatic)
                {
                    ctx.ReportDiagnostic(Diagnostic.Create(
                        Diagnostics.Bit004_InvalidTarget,
                        member.Locations.FirstOrDefault(),
                        member.ToDisplayString(),
                        "Pad cannot be applied to static members"));

                    continue;
                }

                int bits = (int)attr.ConstructorArguments[0].Value!;
                model.Items.Add(new PadModel(bits));
            }

            if (member is IPropertySymbol prop)
                TryParseField(prop, model, ctx);
        }
    }

    private static void TryParseField(IPropertySymbol member, BitLayoutModel model, SourceProductionContext ctx)
    {
        var attr = member.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "Kata.BitFieldAttribute");

        if (attr is null)
            return;

        var location = member.Locations.FirstOrDefault();

        if (!IsValidBitFieldTarget(member, out var reason))
        {
            ctx.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.Bit004_InvalidTarget,
                location,
                member.ToDisplayString(),
                reason));

            return;
        }

        int length = (int)attr.ConstructorArguments[0].Value!;
        int offset = -1;

        foreach (var arg in attr.NamedArguments)
        {
            if (arg.Key != "Offset") continue;
            offset = (int)arg.Value.Value!;
        }

        if (length <= 0)
        {
            ctx.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.Bit007_InvalidLength,
                location,
                "greater than zero",
                length));

            return;
        }

        model.Items.Add(new BitFieldModel(
            member.Name,
            member.Type,
            length,
            offset,
            IsSignedType(member.Type),
            member
        ));
    }


    internal static void ResolveOffsets(BitLayoutModel model)
    {
        int cursor = 0;

        foreach (var item in model.Items)
        {
            switch (item)
            {
                case BitFieldModel f:
                    {
                        int start = f.Offset >= 0 ? f.Offset : cursor;
                        int end = start + f.Length;

                        f.Offset = start;

                        if (end > cursor)
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

        model.ComputedSizeBytes = model.SizeBytes == 0 ? (cursor + 7) / 8 : model.SizeBytes;
    }


    private static bool IsValidBitFieldTarget(IPropertySymbol p, out string? reason)
    {
        if (!IsValidBitFieldType(p.Type))
        {
            reason = $"type '{p.Type.ToDisplayString()}' is not a supported bitfield type";
            return false;
        }

        if (!p.IsPartialDefinition)
        {
            reason = "property must be declared 'partial'";
            return false;
        }

        if (p.IsStatic)
        {
            reason = "property cannot be static";
            return false;
        }

        if (p.Parameters.Length > 0)
        {
            reason = "indexers are not supported";
            return false;
        }

        if (p.GetMethod?.DeclaredAccessibility == Accessibility.Private ||
            p.SetMethod?.DeclaredAccessibility == Accessibility.Private)
        {
            reason = "getters and setters cannot be private";
            return false;
        }

        if (p.DeclaredAccessibility is Accessibility.Protected
            or Accessibility.ProtectedOrInternal)
        {
            reason = "protected members are not supported";
            return false;
        }

        reason = null;
        return true;
    }

    private static bool IsValidBitFieldType(ITypeSymbol? type)
    {
        if (type == null)
            return false;

        if (type.TypeKind == TypeKind.Enum)
            return true;

        return type.SpecialType switch
        {
            SpecialType.System_Byte    => true,
            SpecialType.System_SByte   => true,
            SpecialType.System_Int16   => true,
            SpecialType.System_UInt16  => true,
            SpecialType.System_Int32   => true,
            SpecialType.System_UInt32  => true,
            SpecialType.System_Int64   => true,
            SpecialType.System_UInt64  => true,
            SpecialType.System_Boolean => true,
            _ => false
        };
    }
}


internal abstract class LayoutItem { }

internal sealed class LayoutParseResult(BitLayoutModel? model, Diagnostic? diagnostic)
{
    public BitLayoutModel? Model { get; } = model;
    public Diagnostic? Diagnostic { get; } = diagnostic;
}