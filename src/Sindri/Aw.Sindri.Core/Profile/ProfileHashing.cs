using System.Security.Cryptography;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Profile;

/// <summary>A profile's approval hash: SHA-256 over its canonical JSON (the checked, sorted record as AwJson writes it).</summary>
public static class ProfileHashing
{
    /// <summary>The hash of <paramref name="canonicalJson"/>.</summary>
    /// <param name="canonicalJson">The canonical profile JSON, UTF-8.</param>
    /// <returns>The hash.</returns>
    public static ProfileHash Of(ReadOnlySpan<byte> canonicalJson) =>
        ProfileHash.From(Convert.ToHexStringLower(SHA256.HashData(canonicalJson)));
}
