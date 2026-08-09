using System;

namespace Katera;

/// <summary>
/// Defines layout options for a bitfield struct.
/// Apply to a struct to control the overall size, storage mode, overlap behavior, and bit ordering for its fields.
/// </summary>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class BitLayoutAttribute() : Attribute
{
    /// <summary>
    /// Total size of the layout in bits. A value of 0 indicates the size is
    /// determined automatically based on the declared fields.
    /// </summary>
    public int Size { get; set; } = 0;

    /// <summary>
    /// Controls how the layout is stored or packed. Defaults to <see cref="StorageMode.Auto"/>.
    /// </summary>
    public StorageMode Mode { get; set; } = StorageMode.Auto;

    /// <summary>
    /// When true, allows bit fields to overlap within the layout. Defaults to false.
    /// </summary>
    public bool AllowOverlap { get; set; } = false;

    /// <summary>
    /// The bit ordering used by the layout. Defaults to <see cref="BitOrder.LSBFirst"/>.
    /// </summary>
    public BitOrder BitOrder { get; set; } = BitOrder.LSBFirst;
}