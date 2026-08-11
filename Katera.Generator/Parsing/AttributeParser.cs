using System.Linq;
using Microsoft.CodeAnalysis;
using Katera.Generator.Validation;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Katera.Generator.Utilities.TypeHelpers;

namespace Katera.Generator.Parsing;

internal static class AttributeParser
{
    private const string BitLayoutAttributeName = "Katera.BitLayoutAttribute";
    private const string BitFieldAttributeName  = "Katera.BitFieldAttribute";
    private const string PadAttributeName       = "Katera.PadAttribute";

    internal static ParseResult<BitLayoutModel> ParseLayout(GeneratorAttributeSyntaxContext ctx)
    {
        var symbol      = (INamedTypeSymbol)ctx.TargetSymbol;
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();

        bool isPartial = symbol.DeclaringSyntaxReferences.Any(syntaxRef =>
            syntaxRef.GetSyntax() is TypeDeclarationSyntax declaration &&
            declaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword
        )));

        if (!symbol.IsValueType || !isPartial)
        {
            string reason =
                !symbol.IsValueType ? "target must be a 'struct' or 'record struct'" :
                "struct or record struct must be declared 'partial'";

            diagnostics.Add(new DiagnosticInfo(
                Diagnostics.Kata004_InvalidTarget,
                symbol.Locations.FirstOrDefault()?.ToString(),
                symbol.ToDisplayString(),
                reason
            ));

            return ParseResult<BitLayoutModel>.Failure(diagnostics.ToArray());
        }

        int sizeBits      = 0;
        StorageMode mode  = StorageMode.Auto;
        bool allowOverlap = false;
        BitOrder bitOrder = BitOrder.LSBFirst;
        var bitFieldStubs = ImmutableArray.CreateBuilder<BitFieldStub>();

        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() != BitLayoutAttributeName)
                continue;

            foreach (var arg in attr.NamedArguments)
            {
                switch (arg.Key)
                {
                    case "Size":         sizeBits     = (int)arg.Value.Value!; break;
                    case "Mode":         mode         = (StorageMode)(int)arg.Value.Value!; break;
                    case "AllowOverlap": allowOverlap = (bool)arg.Value.Value!; break;
                    case "BitOrder":     bitOrder     = (BitOrder)(int)arg.Value.Value!; break;
                }
            }
        }

        var itemsBuilder = ImmutableArray.CreateBuilder<LayoutItem>();
        
        foreach (var member in symbol.GetMembers())
        {
            foreach (var attr in member.GetAttributes())
            {
                if (attr.AttributeClass?.ToDisplayString() != PadAttributeName)
                    continue;

                if (member.IsStatic)
                {
                    diagnostics.Add(new DiagnosticInfo(
                        Diagnostics.Kata004_InvalidTarget,
                        member.Locations.FirstOrDefault()?.ToString(),
                        member.ToDisplayString(),
                        "Pad cannot be applied to static members"
                    ));

                    continue;
                }

                int bits = (int)attr.ConstructorArguments[0].Value!;
                itemsBuilder.Add(new PadItem(bits));
            }

            if (member is IPropertySymbol prop)
            {
                if (TryCreateBitFieldStub(prop, out var stub))
                    bitFieldStubs.Add(stub);

                var fieldResult = TryParseField(prop);
                if (fieldResult.Value is not null)
                    itemsBuilder.Add(fieldResult.Value);
                
                diagnostics.AddRange(fieldResult.Diagnostics);
            }
        }

        var items = itemsBuilder.ToImmutable();
        
        var (resolvedItems, sizeBytes) = ResolveOffsets(items, sizeBits);

        BitLayoutModel model = new
        (
            TypeName:          symbol.Name,
            Namespace:         symbol.ContainingNamespace.ToDisplayString(),
            TypeAccessibility: symbol.DeclaredAccessibility,
            IsRecordStruct:    symbol.IsRecord,
            SizeBytes:         sizeBytes,
            Mode:              mode,
            AllowOverlap:      allowOverlap,
            BitOrder:          bitOrder,
            Items:             resolvedItems,
            BitFieldStubs:     bitFieldStubs.ToImmutable()
        );

        return diagnostics.Count > 0
            ? new ParseResult<BitLayoutModel>(model, diagnostics.ToImmutable())
            : ParseResult<BitLayoutModel>.Success(model);
    }

    private static ParseResult<BitFieldItem> TryParseField(IPropertySymbol member)
    {
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        
        var attr = member.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == BitFieldAttributeName);

        if (attr is null)
            return ParseResult<BitFieldItem>.Success(null!);

        var location = member.Locations.FirstOrDefault()?.ToString();

        if (!IsValidBitFieldTarget(member, out var reason))
        {
            diagnostics.Add(new DiagnosticInfo(
                Diagnostics.Kata004_InvalidTarget,
                location,
                member.ToDisplayString(),
                reason!
            ));

            return ParseResult<BitFieldItem>.Failure(diagnostics.ToArray());
        }

        int length = (int)attr.ConstructorArguments[0].Value!;
        int offset = GetNamedIntArgument(attr, "Offset") ?? -1;

        if (length <= 0)
        {
            diagnostics.Add(new DiagnosticInfo(
                Diagnostics.Kata007_InvalidLength,
                location,
                "greater than zero",
                length
            ));

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

    private static bool TryCreateBitFieldStub(IPropertySymbol property, out BitFieldStub stub)
    {
        stub = default!;

        var attr = property.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == BitFieldAttributeName);

        if (attr is null || !property.IsPartialDefinition)
            return false;

        bool hasGetter    = property.GetMethod is not null;
        bool hasSetter    = property.SetMethod is not null;
        bool setterIsInit = property.SetMethod?.IsInitOnly == true;

        stub = new BitFieldStub
        (
            Name:                property.Name,
            TypeDisplayName:     property.Type.ToDisplayString(),
            Accessibility:       property.DeclaredAccessibility,
            IsStatic:            property.IsStatic,
            HasGetter:           hasGetter,
            HasSetter:           hasSetter,
            SetterIsInit:        setterIsInit,
            GetterAccessibility: property.GetMethod?.DeclaredAccessibility ?? property.DeclaredAccessibility,
            SetterAccessibility: property.SetMethod?.DeclaredAccessibility ?? property.DeclaredAccessibility
        );

        return true;
    }

    private static (ImmutableArray<LayoutItem> Items, int SizeBytes) ResolveOffsets(
        ImmutableArray<LayoutItem> items, int declaredSizeBits)
    {
        var resolvedBuilder = ImmutableArray.CreateBuilder<LayoutItem>(items.Length);
        int cursor          = 0;

        foreach (var item in items)
        {
            switch (item)
            {
                case BitFieldItem field:
                {
                    int start = field.Offset >= 0 ? field.Offset : cursor;
                    int end   = start + field.Length;

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

        int sizeBytes = declaredSizeBits == 0 ? (cursor + 7) / 8 : (declaredSizeBits + 7) / 8;
        return (resolvedBuilder.ToImmutable(), sizeBytes);
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

        if (getter is null || setter is null)
            return AccessorKind.GetOnly;

        return setter.IsInitOnly
            ? AccessorKind.GetInit
            : AccessorKind.GetSet;
    }

    private static int? GetNamedIntArgument(AttributeData attribute, string argumentName)
    {
        foreach (var arg in attribute.NamedArguments)
        {
            if (arg.Key == argumentName && arg.Value.Value is int value)
                return value;
        }

        return null;
    }
}