using System.Security.Cryptography;

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
}
