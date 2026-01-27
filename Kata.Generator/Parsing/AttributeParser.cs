using System.Linq;
using Microsoft.CodeAnalysis;
using Kata.Generator.Validation;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Kata.Generator.Utilities.TypeHelpers;

namespace Kata.Generator.Parsing;

internal static class AttributeParser
{
    internal static ParseResult<BitLayoutModel> ParseLayout(GeneratorAttributeSyntaxContext ctx)
    {
        var symbol      = (INamedTypeSymbol)ctx.TargetSymbol;
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();

        bool isPartial = symbol.DeclaringSyntaxReferences.Any(syntaxRef =>
            syntaxRef.GetSyntax() is TypeDeclarationSyntax declaration &&
            declaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)));

        if (!symbol.IsValueType || !isPartial)
        {
            string reason =
                !symbol.IsValueType ? "target must be a struct" :
                "struct must be declared 'partial'";

            diagnostics.Add(new DiagnosticInfo(
                Diagnostics.Bit004_InvalidTarget,
                symbol.Locations.FirstOrDefault()?.ToString(),
                symbol.ToDisplayString(),
                reason));

            return ParseResult<BitLayoutModel>.Failure(diagnostics.ToArray());
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
                    case "Mode":         mode = (StorageMode)(int)arg.Value.Value!; break;
                    case "AllowOverlap": allowOverlap = (bool)arg.Value.Value!; break;
                    case "BitOrder":     bitOrder = (BitOrder)(int)arg.Value.Value!; break;
                }
            }
        }

        var itemsBuilder = ImmutableArray.CreateBuilder<LayoutItem>();
        
        foreach (var member in symbol.GetMembers())
        {
            foreach (var attr in member.GetAttributes())
            {
                if (attr.AttributeClass?.ToDisplayString() != "Kata.PadAttribute")
                    continue;

                if (member.IsStatic)
                {
                    diagnostics.Add(new DiagnosticInfo(
                        Diagnostics.Bit004_InvalidTarget,
                        member.Locations.FirstOrDefault()?.ToString(),
                        member.ToDisplayString(),
                        "Pad cannot be applied to static members"));

                    continue;
                }

                int bits = (int)attr.ConstructorArguments[0].Value!;
                itemsBuilder.Add(new PadItem(bits));
            }

            if (member is IPropertySymbol prop)
            {
                var fieldResult = TryParseField(prop);
                if (fieldResult.Value is not null)
                    itemsBuilder.Add(fieldResult.Value);
                
                diagnostics.AddRange(fieldResult.Diagnostics);
            }
        }

        var items = itemsBuilder.ToImmutable();
        
        var (resolvedItems, computedSize) = ResolveOffsets(items, size);

        BitLayoutModel model = new
        (
            TypeName:          symbol.Name,
            Namespace:         symbol.ContainingNamespace.ToDisplayString(),
            TypeAccessibility: symbol.DeclaredAccessibility,
            SizeBytes:         size,
            Mode:              mode,
            AllowOverlap:      allowOverlap,
            BitOrder:          bitOrder,
            Items:             resolvedItems,
            ComputedSizeBytes: computedSize
        );

        return diagnostics.Count > 0
            ? new ParseResult<BitLayoutModel>(model, diagnostics.ToImmutable())
            : ParseResult<BitLayoutModel>.Success(model);
    }

    private static ParseResult<BitFieldItem> TryParseField(IPropertySymbol member)
    {
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        
        var attr = member.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "Kata.BitFieldAttribute");

        if (attr is null)
            return ParseResult<BitFieldItem>.Success(null!);

        var location = member.Locations.FirstOrDefault()?.ToString();

        if (!IsValidBitFieldTarget(member, out var reason))
        {
            diagnostics.Add(new DiagnosticInfo(
                Diagnostics.Bit004_InvalidTarget,
                location,
                member.ToDisplayString(),
                reason!));

            return ParseResult<BitFieldItem>.Failure(diagnostics.ToArray());
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
            diagnostics.Add(new DiagnosticInfo(
                Diagnostics.Bit007_InvalidLength,
                location,
                "greater than zero",
                length));

            return ParseResult<BitFieldItem>.Failure(diagnostics.ToArray());
        }

        AccessorInfo accessor = new
        (
            member.DeclaredAccessibility,
            GetAccessorKind(member),
            member.IsRequired
        );

        BitFieldItem field = new
        (
            Name:            member.Name,
            TypeDisplayName: member.Type.ToDisplayString(),
            Length:          length,
            Offset:          offset,
            BackingWidth:    GetTypeBitWidth(member.Type),
            IsSigned:        IsSignedType(member.Type),
            Accessor:        accessor
        );

        return ParseResult<BitFieldItem>.Success(field);
    }

    private static (ImmutableArray<LayoutItem> Items, int ComputedSize) ResolveOffsets(
        ImmutableArray<LayoutItem> items, int declaredSize)
    {
        var resolvedBuilder = ImmutableArray.CreateBuilder<LayoutItem>(items.Length);
        int cursor = 0;

        foreach (var item in items)
        {
            switch (item)
            {
                case BitFieldItem field:
                {
                    int start = field.Offset >= 0 ? field.Offset : cursor;
                    int end = start + field.Length;

                    resolvedBuilder.Add(field with { Offset = start });

                    if (end > cursor)
                        cursor = end;

                    break;
                }

                case PadItem pad:
                {
                    resolvedBuilder.Add(pad);
                    cursor += pad.Bits;
                    break;
                }
            }
        }

        int computedSize = declaredSize == 0 ? (cursor + 7) / 8 : declaredSize;
        return (resolvedBuilder.ToImmutable(), computedSize);
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

        if (p.DeclaredAccessibility is Accessibility.Protected or Accessibility.ProtectedOrInternal)
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


    private static AccessorKind GetAccessorKind(IPropertySymbol symbol)
    {
        var getter = symbol.GetMethod;
        var setter = symbol.SetMethod;

        if (getter is null)
            return AccessorKind.GetOnly;

        if (setter is null)
            return AccessorKind.GetOnly;
        else if (setter.IsInitOnly)
            return AccessorKind.GetInit;
        else
            return AccessorKind.GetSet;
    }
}