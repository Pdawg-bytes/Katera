using System;

namespace Katera;

/// <summary>
/// Marks a property as occupying a fixed number of bits within a bit layout.
/// </summary>
/// <param name="length">The number of bits occupied by the field.</param>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class BitFieldAttribute(int length) : Attribute
{
    /// <summary>
    /// The number of bits used by this field.
    /// </summary>
    public int Length { get; } = length;

    /// <summary>
    /// Optional explicit bit offset for this field within the containing layout.
    /// A value of -1 indicates no explicit offset was specified.
    /// </summary>
    public int Offset { get; set; } = -1;
}