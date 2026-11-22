using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Aw.Transcripts.Core.Refs;

/// <summary>Makes the opaque refs: a pure function of the provider and the file's identity, so a ref is stable across runs.</summary>
public static class TranscriptRefs
{
    /// <summary>The ref of one transcript file.</summary>
    /// <param name="provider">The provider that wrote it.</param>
    /// <param name="fileIdentity">The file's full path (what identifies it on this box).</param>
    /// <returns>'transcript:' and the first 8 hex digits of SHA-256(wire name, newline, path).</returns>
    public static SessionRef ForFile(TranscriptProvider provider, string fileIdentity)
    {
        ArgumentNullException.ThrowIfNull(fileIdentity);
        var bytes = Encoding.UTF8.GetBytes($"{TranscriptProviderNames.Of(provider)}\n{fileIdentity}");
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        _ = SHA256.HashData(bytes, hash);
        return SessionRef.From($"transcript:{Convert.ToHexStringLower(hash[..4])}");
    }

    /// <summary>The ref of the turn that starts on <paramref name="line"/> of a session's file.</summary>
    /// <param name="session">The session.</param>
    /// <param name="line">The 1-based line.</param>
    /// <returns>'&lt;session ref&gt;#&lt;line&gt;'.</returns>
    public static TurnRef ForTurn(SessionRef session, int line)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(line, 1);
        return TurnRef.From($"{session.Value}#{line.ToString(CultureInfo.InvariantCulture)}");
    }
}
