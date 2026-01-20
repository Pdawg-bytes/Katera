using System.Linq;
using Microsoft.CodeAnalysis;

namespace Kata.Generator.Parsing;

internal enum AccessorKind
{
    GetOnly,
    GetSet,
    GetInit
}

internal sealed class BitFieldModel : LayoutItem
{
    internal string Name        { get; }
    internal ITypeSymbol Type   { get; }
    internal int Length         { get; }
    internal int Offset         { get; set; }
    internal bool IsSigned      { get; }
    internal Location? Location { get; }

    internal Accessibility Accessibility { get; }
    internal AccessorKind AccessorKind   { get; }
    internal bool IsRequired             { get; }

    internal BitFieldModel(
        string name,
        ITypeSymbol type,
        int length,
        int offset,
        bool isSigned,
        IPropertySymbol symbol)
    {
        Name     = name;
        Type     = type;
        Length   = length;
        Offset   = offset;
        IsSigned = isSigned;
        Location = symbol.Locations.FirstOrDefault();

        Accessibility = symbol.DeclaredAccessibility;
        IsRequired    = symbol.IsRequired;

        var getter = symbol.GetMethod;
        var setter = symbol.SetMethod;

        if (getter is null)
        {
            AccessorKind = AccessorKind.GetOnly;
            return;
        }

        if (setter is null)
            AccessorKind = AccessorKind.GetOnly;
        else if (setter.IsInitOnly)
            AccessorKind = AccessorKind.GetInit;
        else
            AccessorKind = AccessorKind.GetSet;
    }
}