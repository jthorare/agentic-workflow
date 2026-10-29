using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Core.Sandbox.Denials;

/// <summary>Decides the fix of one denial by its kind and rule (sandbox spec §3.6, R3-07); the API puts it on every entry so the app never decides it.</summary>
public static class DenialFixes
{
    /// <summary>The entry with its fix, and with a reason when the fix is 'never'.</summary>
    /// <param name="entry">A counted denial.</param>
    /// <returns>The same entry with <see cref="DenialEntry.Fix"/> and <see cref="DenialEntry.Reason"/> set.</returns>
    public static DenialEntry Decide(DenialEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return entry.Kind switch
        {
            DenialKind.Policy => entry with { Fix = DenialFix.OwnTerminal, Reason = null },
            DenialKind.Network when string.Equals(entry.Rule, DenialRules.PrivateAddress, StringComparison.Ordinal) =>
                entry with { Fix = DenialFix.Never, Reason = "it points at this machine or its own network" },
            DenialKind.Network when string.Equals(entry.Rule, DenialRules.Cap, StringComparison.Ordinal) =>
                entry with { Fix = DenialFix.Never, Reason = "this run used up its connections or bytes, so no host is missing" },
            DenialKind.Network => entry with { Fix = DenialFix.Allow, Reason = null },
            DenialKind.Path when string.Equals(entry.Rule, DenialRules.NotBound, StringComparison.Ordinal) => entry with { Fix = DenialFix.Allow, Reason = null },
            DenialKind.Path => entry with { Fix = DenialFix.Never, Reason = PathReason(entry.Target) },
            _ => throw new ArgumentOutOfRangeException(nameof(entry), entry.Kind, "unknown denial kind"),
        };
    }

    // why: the target is all the counter keeps, so the reason is read off the path the permanent deny list names (Plan/PlanContext.Denied)
    private static string PathReason(string path) => path switch
    {
        _ when Has(path, "docker.sock") || Has(path, "/run/podman") => "it would give the agent full control of this machine",
        _ when Has(path, "/.ssh") || Has(path, "/etc/ssh") || Has(path, "gnupg") || Has(path, "/run/user/") || Has(path, "/Library/Keychains") => "it holds keys that sign as you",
        _ when Has(path, ".agentic-workflow") || Has(path, "sindri") => "it holds Sindri's own data, logins and other runs",
        _ => "it is on the always-blocked list",
    };

    private static bool Has(string path, string part) => path.Contains(part, StringComparison.Ordinal);
}
