namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>Splits a denied directory around the writable paths inside it, so the agent CLI's own read deny doesn't cover the paths a Step writes.</summary>
internal static class DenyCarve
{
    /// <summary>Gets the paths to deny in place of <paramref name="denied"/>.</summary>
    /// <param name="denied">The denied path.</param>
    /// <param name="writable">The Step's writable paths.</param>
    /// <param name="entries">The host's directory listing, or null when it has none.</param>
    /// <returns>The path itself when no writable path lies strictly inside it or the listing is unavailable, else its entries that don't lead to a writable path, level by level.</returns>
    // why: bubblewrap puts an empty tmpfs over a denied directory that a later mount binds into (BubblewrapArgv.Deny) and binds the writable path on top, so a sibling that exists
    // now or appears later is hidden from the session whether or not the agent CLI's rule names it; the carve only stops the CLI's deny from blocking its own Write tool.
    internal static IReadOnlyList<string> Around(string denied, IReadOnlyList<string> writable, Func<string, IReadOnlyList<string>>? entries)
    {
        var inside = writable.Where(w => w.StartsWith(denied.TrimEnd('/') + "/", StringComparison.Ordinal)).ToList();
        if (inside.Count == 0 || entries is null || entries(denied).Count == 0)
        {
            return [denied];
        }

        List<string> paths = [];
        Walk(denied.TrimEnd('/'), inside, entries, paths);
        return paths;
    }

    private static void Walk(string dir, List<string> inside, Func<string, IReadOnlyList<string>> entries, List<string> paths)
    {
        var prefix = dir + "/";
        var leading = inside.Select(w => w[prefix.Length..].Split('/')[0]).ToHashSet(StringComparer.Ordinal);
        paths.AddRange(entries(dir).Where(name => !leading.Contains(name)).Select(name => prefix + name));
        foreach (var name in leading)
        {
            var child = prefix + name;
            var below = inside.Where(w => w.StartsWith(child + "/", StringComparison.Ordinal)).ToList();
            if (below.Count > 0)
            {
                Walk(child, below, entries, paths);
            }
        }
    }
}
