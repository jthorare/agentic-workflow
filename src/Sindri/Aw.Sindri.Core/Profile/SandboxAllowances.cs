using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Core.Profile;

/// <summary>The allowed hosts and home paths a repo's 'sandbox.repos' entry can hold, as the Allow button edits them (sandbox spec §3.6, §3.7).</summary>
public static class SandboxAllowances
{
    /// <summary>The path relative to <paramref name="home"/>, or null when it is not under it (or is the home itself).</summary>
    /// <param name="path">An absolute path.</param>
    /// <param name="home">The owner's home directory.</param>
    /// <returns>The relative path.</returns>
    public static string? HomeRelative(string path, string home)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(home);
        var root = home.TrimEnd('/') + "/";
        if (!path.StartsWith(root, StringComparison.Ordinal) || path.Split('/').Contains(".."))
        {
            return null;
        }

        var relative = path[root.Length..].TrimEnd('/');
        return relative.Length == 0 ? null : relative;
    }

    /// <summary>What is wrong with <paramref name="value"/> as the profile's host or home entry, or null.</summary>
    /// <param name="kind">Network or Path.</param>
    /// <param name="value">The host, or the path relative to the home.</param>
    /// <returns>The problem in the profile's words.</returns>
    public static string? Problem(DenialKind kind, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return kind switch
        {
            DenialKind.Network => SandboxRules.HostProblem(value),
            DenialKind.Path => SandboxRules.HomePathProblem(value),
            DenialKind.Policy => "only a host or a path under your home can be allowed",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown denial kind"),
        };
    }

    /// <summary>Whether the repo's declaration in <paramref name="profile"/> already holds the entry.</summary>
    /// <param name="profile">The profile (the approved one, to know whether the allowance is in force).</param>
    /// <param name="repo">The repo's name.</param>
    /// <param name="kind">Network looks in 'hosts'; Path in 'homeAllow'.</param>
    /// <param name="entry">The host, or the path relative to the home.</param>
    /// <returns>True when it is listed.</returns>
    public static bool Holds(SindriProfile profile, string repo, DenialKind kind, string entry)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Sandbox?.Repos is not { } repos || !repos.TryGetValue(repo, out var declared))
        {
            return false;
        }

        return kind switch
        {
            DenialKind.Network => declared.Hosts?.Contains(entry, StringComparer.Ordinal) == true,
            DenialKind.Path => declared.HomeAllow?.Any(h => string.Equals(h.Path, entry, StringComparison.Ordinal)) == true,
            DenialKind.Policy => false,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown denial kind"),
        };
    }
}
