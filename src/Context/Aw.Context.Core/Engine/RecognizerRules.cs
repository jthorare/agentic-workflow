using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Kernel;

namespace Aw.Context.Core.Engine;

/// <summary>
/// The checks of 'context.extract.recognizers' (S-SK): SND-CONTEXT-131 for the caps, 130 for an entry that can't work, each naming its key path.
/// The kind, glob, pattern and key group are all checked here, so the extractor can trust them.
/// </summary>
internal static partial class RecognizerRules
{
    private const string Path = "context.extract.recognizers";
    private const int MaxRecognizers = 500;
    private const int MaxPattern = 2_000;

    /// <summary>The first refused entry, or null.</summary>
    /// <param name="entries">The entries as written.</param>
    /// <returns>The error, or null when every entry holds.</returns>
    public static Error? Problem(IReadOnlyList<RecognizerEntry?>? entries)
    {
        if (entries is null)
        {
            return null;
        }

        if (entries.Count > MaxRecognizers)
        {
            return Refuse(ContextErrors.RecognizerOverCap, Path, $"has {entries.Count} entries; at most {MaxRecognizers}");
        }

        for (var i = 0; i < entries.Count; i++)
        {
            var at = string.Create(CultureInfo.InvariantCulture, $"{Path}[{i}]");
            if ((entries[i] is { } entry ? Entry(entry, at) : Refuse(ContextErrors.RecognizerInvalid, at, "is empty; write { kind, glob, pattern, keyFrom }")) is { } problem)
            {
                return problem;
            }
        }

        return null;
    }

    private static Error? Entry(RecognizerEntry entry, string at)
    {
        if (entry.Kind is null || !SurfaceKindList.All.Contains(entry.Kind))
        {
            return Refuse(ContextErrors.RecognizerInvalid, at + ".kind", $"is a surface kind: {string.Join(", ", SurfaceKindList.All.Order(StringComparer.Ordinal))}");
        }

        return entry.Glob is null || !SettingsReader.IsGlob(entry.Glob)
            ? Refuse(ContextErrors.RecognizerInvalid, at + ".glob", "is a repo-relative glob (letters, digits, '._-/*', no '..', no leading '/')")
            : Pattern(entry, at);
    }

    private static Error? Pattern(RecognizerEntry entry, string at)
    {
        if (entry.Pattern is { Length: > MaxPattern })
        {
            return Refuse(ContextErrors.RecognizerOverCap, at + ".pattern", $"is over {MaxPattern} characters");
        }

        var regex = Compile(entry.Pattern);
        if (regex is null)
        {
            return Refuse(ContextErrors.RecognizerInvalid, at + ".pattern", "is a regular expression that compiles");
        }

        return entry.KeyFrom is null || !GroupName.IsMatch(entry.KeyFrom) || !regex.GetGroupNames().Contains(entry.KeyFrom, StringComparer.Ordinal)
            ? Refuse(ContextErrors.RecognizerInvalid, at + ".keyFrom", "names a group of the pattern, such as 'name' for (?<name>...)")
            : null;
    }

    private static Regex? Compile(string? pattern)
    {
        try
        {
            return pattern is null ? null : new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static Error Refuse(ErrorDefinition code, string keyPath, string problem) => code.With($"{keyPath}: {problem}");

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]{0,63}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex GroupName { get; }
}
