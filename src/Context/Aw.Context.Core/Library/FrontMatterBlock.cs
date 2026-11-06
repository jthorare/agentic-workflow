using System.Globalization;

namespace Aw.Context.Core.Library;

/// <summary>Finds a design doc's front matter, decides from the raw text of <c language="text">aw-doc</c> whether the file is one, and hands the keys to <see cref="FrontMatterChecker"/> (SND-LIB-010, SND-LIB-019).</summary>
internal static class FrontMatterBlock
{
    /// <summary>What the front matter of a file came to.</summary>
    /// <param name="Read">True when the file is a design doc to read on (it has <c language="text">aw-doc: 1</c> front matter, or an error to show).</param>
    /// <param name="FrontMatter">The front matter when read.</param>
    /// <param name="Errors">The front-matter errors.</param>
    /// <param name="BodyStart">The index of the first body line.</param>
    internal sealed record Outcome(bool Read, DesignDocFrontMatter? FrontMatter, IReadOnlyList<DesignDocError> Errors, int BodyStart);

    /// <summary>Reads the front matter of a file.</summary>
    /// <param name="path">The file's path, for messages.</param>
    /// <param name="lines">The file's lines.</param>
    /// <returns>The outcome.</returns>
    internal static Outcome Find(string path, IReadOnlyList<string> lines)
    {
        var open = 0;
        while (open < lines.Count && lines[open].Trim().Length == 0)
        {
            open++;
        }

        if (open >= lines.Count || !IsDelimiter(lines[open], "---"))
        {
            return Skipped();
        }

        var close = IndexOfClose(lines, open);
        var end = close < 0 ? lines.Count : close;
        var awLine = IndexOfAwDoc(lines, open, end);
        if (awLine < 0)
        {
            return Skipped();
        }

        if (open > 0)
        {
            return Miss(path, awLine + 1, "aw-doc: 1 is after a blank first line, so the file isn't read.", "Make the first line of the file ---.");
        }

        return ReadBlock(path, lines, open, close);
    }

    /// <summary>Makes a finding with the file and line named in its message.</summary>
    /// <param name="path">The file.</param>
    /// <param name="line">The line (from 1); null for the whole file.</param>
    /// <param name="code">The code.</param>
    /// <param name="what">What is wrong.</param>
    /// <param name="fix">What to change.</param>
    /// <returns>The error.</returns>
    internal static DesignDocError Error(string path, int? line, string code, string what, string fix) =>
        new(code, line, line is { } l ? $"{path}:{Number(l)}: {what}" : $"{path}: {what}", fix);

    /// <summary>A number in a message, culture-neutral.</summary>
    /// <param name="n">The number.</param>
    /// <returns>Its digits.</returns>
    internal static string Number(int n) => n.ToString(CultureInfo.InvariantCulture);

    private static Outcome ReadBlock(string path, IReadOnlyList<string> lines, int open, int close)
    {
        var end = close < 0 ? lines.Count : close;
        var entries = FrontMatterReader.Read(lines, open + 1, end);
        var aw = entries.FirstOrDefault(static e => string.Equals(e.Key, "aw-doc", StringComparison.Ordinal));
        if (aw is null)
        {
            return Skipped();
        }

        var miss = NearMiss(path, aw);
        if (miss is not null)
        {
            return miss;
        }

        if (close < 0)
        {
            var error = Error(path, open + 1, DesignDocCodes.BadFrontMatter, "the front matter never closes.", "End the front matter with a --- line.");
            return new Outcome(Read: true, FrontMatter: null, Errors: [error], BodyStart: lines.Count);
        }

        var (fm, errors) = FrontMatterChecker.Check(path, entries, open + 1);
        return new Outcome(Read: true, FrontMatter: fm, Errors: errors, BodyStart: close + 1);
    }

    private static bool IsDelimiter(string line, string delimiter) => string.Equals(line.TrimEnd(), delimiter, StringComparison.Ordinal);

    private static int IndexOfClose(IReadOnlyList<string> lines, int open)
    {
        for (var j = open + 1; j < lines.Count; j++)
        {
            if (IsDelimiter(lines[j], "---") || IsDelimiter(lines[j], "..."))
            {
                return j;
            }
        }

        return -1;
    }

    private static int IndexOfAwDoc(IReadOnlyList<string> lines, int open, int end)
    {
        for (var j = open + 1; j < end; j++)
        {
            if (lines[j].StartsWith("aw-doc", StringComparison.Ordinal) && lines[j]["aw-doc".Length..].TrimStart().StartsWith(':'))
            {
                return j;
            }
        }

        return -1;
    }

    private static Outcome? NearMiss(string path, YamlEntry aw)
    {
        if (aw.Scalar is { Quoted: false } plain && string.Equals(plain.Value, "1", StringComparison.Ordinal))
        {
            return null;
        }

        var shown = aw.Scalar switch
        {
            null => "a list",
            { Quoted: true } s => $"\"{s.Value}\"",
            { Value.Length: 0 } => "empty",
            { } s => s.Value,
        };
        return Miss(path, aw.Line, $"aw-doc is {shown}, not the number 1, so the file isn't read.", "Write aw-doc: 1 with no quotes, on the line after the first ---.");
    }

    private static Outcome Skipped() => new(Read: false, FrontMatter: null, Errors: [], BodyStart: 0);

    private static Outcome Miss(string path, int line, string what, string fix) =>
        new(Read: false, FrontMatter: null, Errors: [Error(path, line, DesignDocCodes.NearMiss, what, fix)], BodyStart: 0);
}
