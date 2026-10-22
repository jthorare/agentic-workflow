using System.Text;

namespace Aw.CodeIndex.Core.Similarity;

/// <summary>The 64-bit FNV-1a hash, spelled out so a signature is the same on every runtime (string.GetHashCode is seeded per process).</summary>
internal static class Fnv1a
{
    private const ulong OffsetBasis = 0xcbf29ce484222325UL;
    private const ulong Prime = 0x100000001b3UL;

    internal static ulong Hash(string text)
    {
        var hash = OffsetBasis;
        foreach (var b in Encoding.UTF8.GetBytes(text))
        {
            hash = (hash ^ b) * Prime;
        }

        return hash;
    }
}
