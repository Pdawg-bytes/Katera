namespace Katera;

/// <summary>
/// Specifies the bit order used when representing layouts.
/// </summary>
public enum BitOrder
{
    /// <summary>
    /// Indicates that the Least Significant Bit (LSB) comes first.
    /// Bit fields are packed starting from the lowest-order bit (bit 0).
    /// </summary>
    LSBFirst,

    /// <summary>
    /// Indicates that the Most Significant Bit (MSB) comes first.
    /// Bit fields are packed starting from the highest-order bit.
    /// </summary>
    MSBFirst
}