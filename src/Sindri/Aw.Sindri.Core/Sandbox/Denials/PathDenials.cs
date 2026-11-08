using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Core.Sandbox.Denials;

/// <summary>Decides whether a path a tool call failed on is a likely denial (sandbox spec §3.6): on the plan's deny list, or under $HOME and not bound.</summary>
public static class PathDenials
{
    /// <summary>Matches one failed path against the plan.</summary>
    /// <param name="plan">The launch's plan.</param>
    /// <param name="path">The absolute path the error named.</param>
    /// <param name="home">The session's $HOME inside the sandbox.</param>
    /// <returns>The denial, or null when the path is genuinely missing, writable or outside $HOME.</returns>
    public static DenialObservation? Match(SandboxPlan plan, string path, string home)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!IsAbsolute(path) || !IsAbsolute(home) || Normalize(path) is not { } p || Normalize(home) is not { } h)
        {
            return null;
        }

        var top = TopMount(plan, p, h);
        if (top is { Kind: PlanMountKind.Deny })
        {
            return new DenialObservation(DenialKind.Path, p, DenialRules.DenyList);
        }

        return top is null && !string.Equals(p, h, StringComparison.Ordinal) && Covers(h, p)
            ? new DenialObservation(DenialKind.Path, p, DenialRules.NotBound)
            : null;
    }

    private static bool IsAbsolute(string path) => !string.IsNullOrEmpty(path) && path[0] == '/';

    /// <summary>The mount stack is bottom to top: the last entry that covers the path decides how it appears (the home tmpfs itself binds nothing).</summary>
    private static PlanMount? TopMount(SandboxPlan plan, string path, string home)
    {
        PlanMount? top = null;
        foreach (var mount in plan.Mounts)
        {
            var root = Normalize(mount.Path);
            if (mount.Kind is not (PlanMountKind.Dev or PlanMountKind.Proc) && Covers(root, path) && !string.Equals(root, home, StringComparison.Ordinal))
            {
                top = mount;
            }
        }

        return top;
    }

    private static bool Covers(string? root, string path) =>
        root is not null && (string.Equals(root, path, StringComparison.Ordinal) || path.StartsWith(root == "/" ? "/" : root + "/", StringComparison.Ordinal));

    private static string? Normalize(string path)
    {
        List<string> parts = [];
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (parts.Count == 0)
                {
                    return null;
                }

                parts.RemoveAt(parts.Count - 1);
                continue;
            }

            parts.Add(segment);
        }

        return "/" + string.Join('/', parts);
    }
}
