using System.ComponentModel;

namespace System.Runtime.CompilerServices
{
    // This is to allow for record types and init-only properties in projects targeting netstandard2.0
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal class IsExternalInit
    {
    }
}