namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>
/// The install tree behind a linked agent binary (#2149). The sandbox's home is empty but for a few binds, so a link in ~/.local/bin that points into
/// ~/.local/share/claude/versions/x dangles inside it and the exec fails with 127. The host resolves the link; this finds the folder to bind for it.
/// </summary>
internal static class AgentBinaryTree
{
    private const string ShareFolder = "/.local/share/";

    /// <summary>The links of the launch's own agent binary, as (PATH entry, real path); other agents' links are not this launch's business.</summary>
    /// <param name="c">The plan context.</param>
    /// <returns>The links that point into the owner's home.</returns>
    internal static IReadOnlyList<(string Link, string Real)> Links(PlanContext c)
    {
        var name = BinaryName(c.Request.Target.Value);
        return [.. (c.Request.Host.BinaryLinks ?? new Dictionary<string, string>(StringComparer.Ordinal))
            .Where(l => string.Equals(l.Key[(l.Key.LastIndexOf('/') + 1)..], name, StringComparison.Ordinal) && l.Value.StartsWith(c.Home + "/", StringComparison.Ordinal))
            .OrderBy(l => l.Key, StringComparer.Ordinal)
            .Select(l => (l.Key, l.Value))];
    }

    /// <summary>The folder to bind read-only: ~/.local/share/&lt;name&gt; when the binary lives there, else the first folder under the home.</summary>
    /// <param name="home">The owner's home.</param>
    /// <param name="real">The binary's real path.</param>
    /// <returns>The install tree.</returns>
    internal static string Of(string home, string real)
    {
        var share = home + ShareFolder;
        var prefix = real.StartsWith(share, StringComparison.Ordinal) ? share : home + "/";
        var rest = real[prefix.Length..];
        var slash = rest.IndexOf('/', StringComparison.Ordinal);
        return prefix + (slash < 0 ? rest : rest[..slash]);
    }

    private static string BinaryName(string target) => string.Equals(target, "claude-code", StringComparison.Ordinal) ? "claude" : target;
}
