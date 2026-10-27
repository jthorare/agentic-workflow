using System.Text.RegularExpressions;
using Aw.Kernel;

namespace Aw.Context.Core.Engine;

/// <summary>The checks of 'context.docs.directoryNames' (#1373): plain file names, matched in any directory.</summary>
internal static partial class DocNameRules
{
    /// <summary>The first entry that isn't a plain file name, or null. An empty list is allowed: it leaves only the globs.</summary>
    /// <param name="docs">The 'context.docs' section, or null.</param>
    /// <returns>SND-CONTEXT-001 with the key path, or null.</returns>
    internal static Error? Problem(DocsSection? docs)
    {
        var names = docs?.DirectoryNames ?? [];
        var bad = names.ToList().FindIndex(n => n is null || !FileName.IsMatch(n));
        return bad < 0
            ? null
            : SettingsReader.Invalid("context.docs.directoryNames", $"'{(names[bad] is { } name ? SettingsReader.Printable(name) : "(empty)")}' isn't a file name (letters, digits and '._-', no '/')");
    }

    /// <summary>The names the build matches: the profile's list (every entry was checked by <see cref="Problem"/>), else the defaults.</summary>
    /// <param name="docs">The 'context.docs' section, or null.</param>
    /// <returns>The names.</returns>
    internal static IReadOnlyList<string> Names(DocsSection? docs) =>
        docs?.DirectoryNames is { } written ? [.. written.OfType<string>()] : ContextSettings.DefaultDocDirectoryNames;

    [GeneratedRegex("^[A-Za-z0-9._-]{1,200}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FileName { get; }
}
