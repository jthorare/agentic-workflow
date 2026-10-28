using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Kernel;

namespace Aw.Context.Core.Engine;

/// <summary>'context.docs.directoryNames': file names that count as docs in any directory. A name is a plain file name, so '**/name' is the only glob it can make.</summary>
internal static partial class DocNamesRules
{
    private const int Most = 50;

    /// <summary>The names to use: the written ones, or the defaults when the key is left out.</summary>
    /// <param name="docs">The 'context.docs' section, or null.</param>
    /// <returns>The names.</returns>
    internal static IReadOnlyList<string> Names(DocsSection? docs) =>
        docs?.DirectoryNames is { } written ? [.. written.OfType<string>()] : ContextSettings.DefaultDocDirectoryNames;

    /// <summary>The first problem with the written names, or null.</summary>
    /// <param name="names">The written names, or null when the key is left out.</param>
    /// <returns>SND-CONTEXT-001 at the key path, or null.</returns>
    internal static Error? Problem(IReadOnlyList<string?>? names)
    {
        const string key = "context.docs.directoryNames";
        if (names is null)
        {
            return null;
        }

        if (names.Count > Most)
        {
            return SettingsReader.Invalid(key, $"at most {Most.ToString(CultureInfo.InvariantCulture)} names (has {names.Count.ToString(CultureInfo.InvariantCulture)})");
        }

        var bad = names.Where(n => n is null || !Shape.IsMatch(n) || string.Equals(n, ".", StringComparison.Ordinal) || string.Equals(n, "..", StringComparison.Ordinal)).Take(1).ToList();
        if (bad.Count > 0)
        {
            return SettingsReader.Invalid(key, $"'{(bad[0] is { } text ? SettingsReader.Printable(text) : "(empty)")}' isn't a plain file name (letters, digits, '._-', no '/' or '*')");
        }

        var twice = names.OfType<string>().GroupBy(n => n, StringComparer.Ordinal).Where(g => g.Skip(1).Any()).Select(g => g.Key).Take(1).ToList();
        return twice.Count > 0 ? SettingsReader.Invalid(key, $"'{SettingsReader.Printable(twice[0])}' is listed twice") : null;
    }

    [GeneratedRegex("^[A-Za-z0-9._-]{1,255}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
