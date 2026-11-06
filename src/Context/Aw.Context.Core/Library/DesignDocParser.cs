namespace Aw.Context.Core.Library;

/// <summary>
/// Turns a design doc's cleaned text into its front matter, items, refs and errors (spec 2026-11-04 §4.1, §4.2, §5.3).
/// Pure: no store, no I/O. The codes decided against the rest of the scope (012, 015, 016) are <see cref="DesignDocScope"/>'s.
/// </summary>
public static class DesignDocParser
{
    /// <summary>The parser's version; a new one re-reads every doc.</summary>
    public const int Version = 1;

    /// <summary>The largest doc read, in bytes of UTF-8.</summary>
    public const int MaxBytes = 1024 * 1024;

    /// <summary>The most items one doc holds.</summary>
    public const int MaxItems = 2000;

    /// <summary>The most characters an item's text keeps.</summary>
    public const int MaxTextChars = 4000;

    /// <summary>Parses one doc.</summary>
    /// <param name="path">The doc's path (named in messages).</param>
    /// <param name="text">Its cleaned text.</param>
    /// <param name="actualSourceSha256">The sha256 of the source a generated doc was made from, when known; a mismatch with the doc's <c language="text">source-sha256</c> is SND-LIB-017.</param>
    /// <returns>The parsed doc.</returns>
    public static ParsedDesignDoc Parse(string path, string text, string? actualSourceSha256 = null) =>
        throw new NotSupportedException("red: DesignDocParser.Parse");
}
