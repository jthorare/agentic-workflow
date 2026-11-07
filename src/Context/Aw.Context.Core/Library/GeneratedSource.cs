using System.Security.Cryptography;
using System.Text;

namespace Aw.Context.Core.Library;

/// <summary>
/// Where a generated design doc's source is (spec 2026-11-04, system-design row): <c language="text">write_packet.py</c> writes the doc from the <c language="text">packet.json</c> in the same folder,
/// so SND-LIB-017 compares the doc's <c language="text">source-sha256</c> with the hash of that file. A doc with no <c language="text">packet.json</c> beside it is not checked (no finding).
/// </summary>
public static class GeneratedSource
{
    /// <summary>The name of the source file beside a generated doc.</summary>
    public const string FileName = "packet.json";

    /// <summary>True when the front matter marks the doc as generated and names the source's hash.</summary>
    /// <param name="frontMatter">The doc's parsed front matter, or null when it has none.</param>
    /// <returns>Whether the doc has both <c language="text">generated</c> and <c language="text">source-sha256</c>.</returns>
    public static bool IsGenerated(DesignDocFrontMatter? frontMatter) => frontMatter is { Generated: not null, SourceSha256: not null };

    /// <summary>The path of the source file beside a doc.</summary>
    /// <param name="docPath">The doc's path.</param>
    /// <returns>The <c language="text">packet.json</c> in the doc's folder.</returns>
    public static string PathBeside(string docPath)
    {
        ArgumentNullException.ThrowIfNull(docPath);
        var cut = docPath.AsSpan().LastIndexOfAny('/', '\\');   // Core has no System.IO: the folder is everything up to the last separator
        return cut < 0 ? FileName : string.Concat(docPath.AsSpan(0, cut + 1), FileName);
    }

    /// <summary>The hash a doc's <c language="text">source-sha256</c> is compared with.</summary>
    /// <param name="source">The source file's bytes.</param>
    /// <returns>Their sha256, lower-case hex.</returns>
    public static string Sha256(ReadOnlySpan<byte> source) => Convert.ToHexStringLower(SHA256.HashData(source));

    /// <summary>The first character of a version <see cref="Version"/> made, so a stored version tells a folded one from a plain doc id.</summary>
    public const char Mark = 'g';

    /// <summary>The version of a doc read with the packet.json beside it: one rule for every feed, so a change to either file is a new version.</summary>
    /// <param name="docVersion">The doc's own version (its sha256 or blob id).</param>
    /// <param name="packetVersion">The packet.json's sha256 or blob id, or null when there is none beside the doc.</param>
    /// <returns>The mark and the sha256 of the two joined.</returns>
    public static string Version(string docVersion, string? packetVersion)
    {
        ArgumentNullException.ThrowIfNull(docVersion);
        return Mark + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(docVersion + ":" + packetVersion)));
    }
}
