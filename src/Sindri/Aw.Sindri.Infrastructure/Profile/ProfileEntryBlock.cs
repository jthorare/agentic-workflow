namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>
/// The line-level core of the profile's list edits: finds a block sequence by its key path ('index', 'concepts'), then inserts an entry after the last one, replaces the entry whose 'id:' matches in place, or removes it.
/// An entry is its '- ' line down to its last content line: a comment inside it goes with it, a comment above it stays. An entry with no 'id:' is matched only by the name an edit gives for it, and only when no entry has the id. Every method returns false for a layout it won't edit
/// (a flow value with content, anchors, block scalars, tabs, a flow-map entry); the caller then refuses and writes nothing.
/// </summary>
internal static class ProfileEntryBlock
{
    /// <summary>Sets the entry with <paramref name="id"/>: replaced in place when there, otherwise inserted after the last entry (creating the keys on the path when they are missing).</summary>
    /// <param name="lines">The file's lines, edited in place.</param>
    /// <param name="keyPath">The keys down to the list, from the top level: 'index', 'concepts'.</param>
    /// <param name="id">The entry's id.</param>
    /// <param name="render">The entry's lines for the item indent it is given; the first starts with '- '.</param>
    /// <param name="matchName">The name of an entry with no id to replace when no entry has <paramref name="id"/>, or null.</param>
    /// <returns>False when the layout is one this edit leaves alone.</returns>
    internal static bool Set(List<string> lines, string[] keyPath, string id, Func<int, string[]> render, string? matchName = null)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(render);
        if (ProfileBlockLocator.HasTab(lines))
        {
            return false;
        }

        var found = ProfileBlockLocator.Locate(lines, keyPath, render, out var list);
        if (found != ProfileBlockLocator.Found.Yes)
        {
            return found == ProfileBlockLocator.Found.Created;   // Created: the keys and the entry were added already
        }

        if (ProfileEntryScan.Entries(lines, list) is not { } entries)
        {
            return false;
        }

        var match = Match(entries, id, matchName);
        if (match is null)
        {
            lines.InsertRange(list.ContentEnd, render(list.ItemIndent));
            return true;
        }

        lines.RemoveRange(match.Start, match.End - match.Start);
        lines.InsertRange(match.Start, render(list.ItemIndent));
        return true;
    }

    /// <summary>Removes the entry with <paramref name="id"/>; nothing when there is none. A list left empty becomes 'key: []'.</summary>
    /// <param name="lines">The file's lines, edited in place.</param>
    /// <param name="keyPath">The keys down to the list.</param>
    /// <param name="id">The entry's id.</param>
    /// <param name="matchName">The name of an entry with no id to remove when no entry has <paramref name="id"/>, or null.</param>
    /// <returns>False when the layout is one this edit leaves alone.</returns>
    internal static bool Remove(List<string> lines, string[] keyPath, string id, string? matchName = null)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (ProfileBlockLocator.HasTab(lines))
        {
            return false;
        }

        var found = ProfileBlockLocator.Locate(lines, keyPath, create: null, out var list);
        if (found != ProfileBlockLocator.Found.Yes)
        {
            return found == ProfileBlockLocator.Found.Missing;
        }

        if (ProfileEntryScan.Entries(lines, list) is not { } entries)
        {
            return false;
        }

        var match = Match(entries, id, matchName);
        if (match is null)
        {
            return true;
        }

        lines.RemoveRange(match.Start, match.End - match.Start);
        if (entries.Count == 1)
        {
            lines[list.KeyLine] = new string(' ', list.KeyIndent) + keyPath[^1] + ": []";
        }

        return true;
    }

    private static ProfileEntryScan.EntryAt? Match(List<ProfileEntryScan.EntryAt> entries, string id, string? matchName) =>
        entries.Find(e => e.Id == id) ?? (matchName is null ? null : entries.Find(e => e.Id is null && e.Name == matchName));
}
