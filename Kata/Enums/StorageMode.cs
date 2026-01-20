namespace Kata;

public enum StorageMode
{
    /// <summary>
    /// Automatically determine the <see cref="StorageMode"/> of the layout. If the layout is 1, 2, 4, or 8 bytes in size, <see cref="StorageMode.Register"/> is used; otherwise, <see cref="StorageMode.Blob"/> is used.
    /// </summary>
    Auto,

    /// <summary>
    /// Creates a <c>byte</c>, <c>ushort</c>, <c>uint</c>, or <c>ulong</c> to store the data efficiently.
    /// </summary>
    Register,

    /// <summary>
    /// Stores the data in an <c>InlineArray</c> of the amount of bytes the layout consumes.
    /// </summary>
    Blob,

    /// <summary>
    /// Stores the data in real C# fields with no unified backing storage.
    /// </summary>
    Expanded
}