using System;

namespace Katera;

/// <summary>
/// Adds unnamed padding to a bit layout.
/// Use this attribute on structs, fields, or properties to reserve space between defined bit fields.
/// </summary>
/// <param name="bits">The number of bits to reserve as padding.</param>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
public sealed class PadAttribute(int bits) : Attribute
{
    /// <summary>
    /// The number of bits reserved by this padding attribute.
    /// </summary>
    public int Bits { get; } = bits;
}