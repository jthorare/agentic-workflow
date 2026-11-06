using System.Globalization;
using System.Text;

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

    /// <summary>Stamps a scrub ruleset with the parser's version, so a record stored under an older parser no longer matches and is read again.</summary>
    /// <param name="ruleset">The scrub ruleset's value.</param>
    /// <returns>The ruleset followed by the parser version.</returns>
    public static string Stamp(string ruleset) => $"{ruleset}+aw-doc-p{Version.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>What a design doc the scrubber blocked comes to: no front matter, no items, and the error "blocked" (SND-LIB-018).</summary>
    /// <param name="path">The doc's path, for the message.</param>
    /// <returns>The parsed shape of a blocked doc.</returns>
    public static ParsedDesignDoc Blocked(string path) =>
        new(Version, FrontMatter: null, Items: [], Refs: [], Errors: [FrontMatterBlock.Error(path, line: null, DesignDocCodes.TooLarge, "blocked: the scrubber withheld the text.", "Remove what the scrubber hit.")]);

    /// <summary>Says whether a parse is of a design doc: it has front matter, or an error to show. A file with no <c language="text">aw-doc</c> front matter is not one.</summary>
    /// <param name="doc">The parse.</param>
    /// <returns>True for a design doc.</returns>
    public static bool IsDesignDoc(ParsedDesignDoc doc) => doc is { FrontMatter: not null } or { Errors.Count: > 0 };

    /// <summary>Parses one doc.</summary>
    /// <param name="path">The doc's path (named in messages).</param>
    /// <param name="text">Its cleaned text.</param>
    /// <param name="actualSourceSha256">The sha256 of the source a generated doc was made from, when known; a mismatch with the doc's <c language="text">source-sha256</c> is SND-LIB-017.</param>
    /// <returns>The parsed doc.</returns>
    public static ParsedDesignDoc Parse(string path, string text, string? actualSourceSha256 = null)
    {
        var lines = text.TrimStart('﻿').ReplaceLineEndings("\n").Split('\n');
        var head = FrontMatterBlock.Find(path, lines);
        if (!head.Read || head.FrontMatter is null)
        {
            return new ParsedDesignDoc(Version, FrontMatter: null, Items: [], Refs: [], Errors: [.. head.Errors]);
        }

        var fm = head.FrontMatter;
        var errors = new List<DesignDocError>(head.Errors);
        if (fm.Generated is not null && fm.SourceSha256 is not null && actualSourceSha256 is not null
            && !string.Equals(fm.SourceSha256, actualSourceSha256, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(FrontMatterBlock.Error(path, fm.KeyLines["source-sha256"], DesignDocCodes.StaleGenerated, "source-sha256 no longer matches the source: the generated file was edited by hand.", "Edit the source and regenerate the file."));
        }

        if (Encoding.UTF8.GetByteCount(text) > MaxBytes)
        {
            errors.Add(TooLarge(path, "the file is over 1 MiB."));
            return new ParsedDesignDoc(Version, fm, Items: [], Refs: [], Errors: Sorted(errors));
        }

        var walker = new DesignDocWalker(path, lines, head.BodyStart, fm.Kind);
        var (items, refs, bodyErrors) = walker.Walk();
        errors.AddRange(bodyErrors);
        if (walker.ItemCount > MaxItems)
        {
            errors.Add(TooLarge(path, $"the file has more than {MaxItems} items."));
        }

        return errors.Count > 0
            ? new ParsedDesignDoc(Version, fm, Items: [], Refs: [], Errors: Sorted(errors))
            : new ParsedDesignDoc(Version, fm, items, refs, Errors: []);
    }

    private static DesignDocError TooLarge(string path, string what) =>
        FrontMatterBlock.Error(path, line: null, DesignDocCodes.TooLarge, "too large: " + what, "Split the doc.");

    private static List<DesignDocError> Sorted(List<DesignDocError> errors) =>
        [.. errors.OrderBy(static e => e.Line ?? int.MaxValue).ThenBy(static e => e.Code, StringComparer.Ordinal)];
}
