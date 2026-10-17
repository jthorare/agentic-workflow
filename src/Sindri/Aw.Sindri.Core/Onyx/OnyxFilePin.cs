using System.Security.Cryptography;
using System.Text;
using Aw.Kernel;

namespace Aw.Sindri.Core.Onyx;

/// <summary>One pinned deployment file of the Onyx release (a row `file` of context/onyx.lock).</summary>
/// <param name="Path">The path under the release's deployment/ directory.</param>
/// <param name="Sha256">The file's sha256, 64 lowercase hex digits.</param>
public sealed record OnyxFilePin(string Path, string Sha256)
{
    /// <summary>Hashes content the way the lock does.</summary>
    /// <param name="content">The file's bytes.</param>
    /// <returns>The sha256 as 64 lowercase hex digits.</returns>
    public static string Sha256Of(ReadOnlySpan<byte> content) => Convert.ToHexStringLower(SHA256.HashData(content));

    /// <summary>Checks content against the pin without leaking where it differs.</summary>
    /// <param name="content">The file's bytes.</param>
    /// <returns>True when its sha256 is the pinned one.</returns>
    public bool Matches(ReadOnlySpan<byte> content)
        => CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Sha256Of(content)), Encoding.ASCII.GetBytes(Sha256));

    /// <summary>The error for content that fails the pin: SND-ONYX-004 naming the file and the first 12 digits of both hashes.</summary>
    /// <param name="content">The bytes that were served.</param>
    /// <returns>The error.</returns>
    public Error Mismatch(ReadOnlySpan<byte> content)
        => SindriErrors.OnyxPinsMalformed.With($"{Path} from the pinned commit doesn't match context/onyx.lock (got {Sha256Of(content)[..12]}, pinned {Sha256[..12]}); nothing was written for it, so check your network or proxy and run `sindri onyx up` again");

    /// <summary>The error for a file that couldn't be fetched: SND-ONYX-004 naming the file and why.</summary>
    /// <param name="why">What went wrong, such as `HTTP 404`.</param>
    /// <returns>The error.</returns>
    public Error Unavailable(string why)
        => SindriErrors.OnyxPinsMalformed.With($"{Path} couldn't be fetched from the pinned commit ({why}); nothing was written for it, so check your network or proxy and run `sindri onyx up` again");
}
