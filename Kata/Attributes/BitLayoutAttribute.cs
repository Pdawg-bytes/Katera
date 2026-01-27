using System;

namespace Kata;

[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class BitLayoutAttribute() : Attribute
{
    public int Size { get; set; }          = 0;
    public StorageMode Mode { get; set; }  = StorageMode.Auto;
    public bool AllowOverlap { get; set; } = false;
    public BitOrder BitOrder { get; set; } = Kata.BitOrder.LSBFirst;
}