using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The text of 'sindri sandbox show' and 'sindri sandbox explain': pure over the plan, so a session can ask with the state directory denied.</summary>
public static partial class SandboxPlanRenderer
{
    private static readonly string[] _fileExtensions = ["sh", "md", "exe", "cs", "js", "ts", "json", "txt", "yml", "yaml", "py", "rb", "go", "rs", "toml", "xml", "html", "css", "log", "lock", "csproj", "slnx", "mjs", "cjs"];

    private const string Always = "always blocked: ";

    private const string RealHome = "the owner's real home";   // SeatbeltPlan's reason for denying it

    /// <summary>The whole plan as text.</summary>
    /// <param name="plan">The plan.</param>
    /// <returns>The text.</returns>
    public static string Show(SandboxPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var text = new StringBuilder();
        var kind = plan.Attended ? "session you opened" : "Step";
        _ = text.AppendLine(CultureInfo.InvariantCulture, $"Sandbox {plan.PlanHash} ({SandboxWire.Name(plan.Mechanism)}) for a {kind}");
        _ = text.AppendLine(CultureInfo.InvariantCulture, $"Launch: {plan.Launch.Value}");
        _ = text.AppendLine().AppendLine("Files, bottom to top (a later line wins):");
        foreach (var mount in plan.Mounts)
        {
            _ = text.AppendLine(CultureInfo.InvariantCulture, $"  {Label(mount.Kind),-9} {mount.Path}  ({mount.Reason})");
        }

        _ = text.AppendLine().AppendLine(CultureInfo.InvariantCulture, $"Network ({SandboxWire.Name(plan.Network.Mode)}{(LearnDate(plan) is { } date ? ", learning until " + date : string.Empty)}):");
        foreach (var group in plan.Network.Groups)
        {
            _ = text.AppendLine(CultureInfo.InvariantCulture, $"  {group.Name}: {string.Join(", ", group.Hosts)}");
        }

        _ = text.AppendLine().AppendLine("Environment (names only):").Append("  ").AppendJoin(", ", EnvironmentNames(plan)).AppendLine();
        _ = text.AppendLine().AppendLine("Limits:").Append("  ").AppendLine(LimitsLine(plan));
        _ = text.AppendLine().AppendLine(CultureInfo.InvariantCulture, $"Credentials: GitHub {SandboxWire.Name(plan.Github)}, model login {SandboxWire.Name(plan.Login)}");
        _ = text.AppendLine(CultureInfo.InvariantCulture, $"The agent's own policy: {(plan.Policy.Mode == InnerMode.Ask ? "asks" : "refuses")}, {plan.Policy.Rules.Count} rules; its own sandbox: {SandboxWire.Name(plan.Inner)}");
        _ = text.AppendLine(CultureInfo.InvariantCulture, $"Hooks kept: {string.Join(", ", plan.Hooks.Kept)}");
        _ = text.AppendLine(CultureInfo.InvariantCulture, $"Hooks left out: {(plan.Hooks.Omitted.Count == 0 ? "none" : string.Join(", ", plan.Hooks.Omitted))}");
        foreach (var note in plan.Hooks.Notes)
        {
            _ = text.AppendLine(CultureInfo.InvariantCulture, $"Note: {note}");
        }

        return text.ToString();
    }

    /// <summary>The four lines 'sindri sandbox show' leads with: what the session can write, what it can read, which network it has and its limits.</summary>
    /// <param name="plan">The plan.</param>
    /// <returns>The four lines.</returns>
    public static string Lead(SandboxPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var readable = plan.Mounts.Where(static m => m.Kind == PlanMountKind.ReadOnly).Select(static m => m.Path).Distinct(StringComparer.Ordinal).ToList();
        var shown = string.Join(", ", readable.Take(4)) + (readable.Count > 4 ? string.Create(CultureInfo.InvariantCulture, $" and {readable.Count - 4} more") : string.Empty);
        var network = string.Join(", ", plan.Network.Groups.Select(static g => g.Name));
        return $"Can write: {(plan.Writable.Count == 0 ? "nothing" : string.Join(", ", plan.Writable))}\nCan read: {(readable.Count == 0 ? "only what it can write" : shown)}\nNetwork: {SandboxWire.Name(plan.Network.Mode)}, {(network.Length == 0 ? "no groups" : network)}\nLimits: {LimitsLine(plan)}\n";
    }

    /// <summary>Why a path, a host or a command is allowed or denied, and how the owner can change it.</summary>
    /// <param name="plan">The plan.</param>
    /// <param name="query">A path, a host name or a command.</param>
    /// <returns>The answer.</returns>
    public static string Explain(SandboxPlan plan, string query)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(query);
        var q = query.Trim();
        if (q.Length == 0)
        {
            return "Ask about a path, a host or a command, for example: sindri sandbox explain api.github.com\n";
        }

        if (q.StartsWith('/') || q.StartsWith('~'))
        {
            return ExplainPath(plan, q);
        }

        return LooksLikeHost(q) ? ExplainHost(plan, q) : ExplainCommand(plan, q);
    }

    private static List<string> EnvironmentNames(SandboxPlan plan)
    {
        var names = plan.Environment.Select(e => e.Name).Where(n => !n.StartsWith("GIT_CONFIG_KEY_", StringComparison.Ordinal) && !n.StartsWith("GIT_CONFIG_VALUE_", StringComparison.Ordinal)).ToList();
        var at = names.IndexOf("GIT_CONFIG_COUNT");
        names.Insert(at + 1, "GIT_CONFIG_KEY_n and GIT_CONFIG_VALUE_n");
        return names;
    }

    private static string Label(PlanMountKind kind) => kind switch
    {
        PlanMountKind.ReadOnly => "read-only",
        PlanMountKind.ReadWrite => "writable",
        PlanMountKind.Deny => "blocked",
        PlanMountKind.Tmpfs => "empty",
        PlanMountKind.PerLaunchDir => "this run",
        PlanMountKind.Dev => "device",
        PlanMountKind.Proc => "process",
        PlanMountKind.Link => "link",
        _ => kind.ToString(),
    };

    private static string LimitsLine(SandboxPlan plan)
    {
        var l = plan.Limits;
        var home = plan.Mechanism == SandboxMechanism.Seatbelt ? string.Empty : $", home {SandboxUnits.FormatSize(l.HomeTmpfsBytes)}";   // a Seatbelt home is a folder of links, not a sized tmpfs
        var wall = l.Wall is { } w ? SandboxUnits.FormatDuration(w) : "none";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"memory {SandboxUnits.FormatSize(l.MemoryMaxBytes)} (slowed at {SandboxUnits.FormatSize(l.MemoryHighBytes)}), cpu {l.CpuQuotaPercent}%, processes {l.TasksMax}, sessions {l.Sessions}{home}, time {wall}");
    }

    private static string ExplainPath(SandboxPlan plan, string query)
    {
        var seatbelt = plan.Mechanism == SandboxMechanism.Seatbelt;
        var home = HomeOf(plan, seatbelt);
        var path = Normalize(query.StartsWith('~') ? home + query[1..] : query);
        path = seatbelt ? ThroughLink(plan, path) : path;
        var mount = plan.Mounts.LastOrDefault(m => string.Equals(m.Path, path, StringComparison.Ordinal) || path.StartsWith(m.Path.TrimEnd('/') + "/", StringComparison.Ordinal));
        return mount switch
        {
            null => $"{path} is not listed; the system is read-only.\n",
            { Kind: PlanMountKind.Deny } when seatbelt && mount.Reason.StartsWith(RealHome, StringComparison.Ordinal) => $"{path} is blocked: on macOS the real home is denied; only the entries listed here are allowed.\nThe owner can allow it for this repo: add it to sandbox.repos.<repo>.homeAllow in the profile.\n",
            { Kind: PlanMountKind.Deny } => ExplainDenied(path, mount),
            { Kind: PlanMountKind.Tmpfs } when !string.Equals(mount.Path, home, StringComparison.Ordinal) => ExplainDenied(path, new PlanMount(path, PlanMountKind.Deny, Reason: Always + "Sindri's own state is not visible to a session")),
            { Kind: PlanMountKind.Tmpfs } => $"{path} is not bound in the sandbox home, so it does not exist for this session (a tool that writes there loses it with the run).\nThe owner can allow it for this repo: add it to sandbox.repos.<repo>.homeAllow in the profile.\n",
            { Kind: PlanMountKind.ReadOnly } => $"{path} can be read, not changed ({mount.Reason}).\n",
            _ => $"{path} is writable ({mount.Reason}).\n",
        };
    }

    // The home is the first tmpfs other than /run (SB-2 lists the empty /run before it); on Seatbelt it is HOME, the run's folder of links.
    private static string HomeOf(SandboxPlan plan, bool seatbelt) => seatbelt
        ? plan.Environment.FirstOrDefault(static e => string.Equals(e.Name, "HOME", StringComparison.Ordinal))?.Value ?? string.Empty
        : plan.Mounts.FirstOrDefault(m => m.Kind == PlanMountKind.Tmpfs && !string.Equals(m.Path, "/run", StringComparison.Ordinal))?.Path ?? string.Empty;

    // A path through a link in the run's home is the link's target: the rules are on the real path.
    private static string ThroughLink(SandboxPlan plan, string path)
    {
        var link = plan.Mounts.LastOrDefault(m => m is { Kind: PlanMountKind.Link, Source: not null } && (string.Equals(m.Path, path, StringComparison.Ordinal) || path.StartsWith(m.Path.TrimEnd('/') + "/", StringComparison.Ordinal)));
        return link?.Source is { } target ? target + path[link.Path.Length..] : path;
    }

    private static string ExplainDenied(string path, PlanMount mount)
    {
        var fix = mount.Reason.StartsWith(Always, StringComparison.Ordinal)
            ? "No setting changes this. If you need it, ask the owner to do it in their own terminal."
            : "The owner can lift it by removing it from the profile.";
        var reason = mount.Reason.StartsWith(Always, StringComparison.Ordinal) ? mount.Reason[Always.Length..] : mount.Reason;
        return $"{path} is blocked: {reason}.\n{fix}\n";
    }

    private static string? LearnDate(SandboxPlan plan) =>
        plan.Network is { Mode: NetworkMode.Learn, LearnUntil: { } until } ? until.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null;

    private static string ExplainHost(SandboxPlan plan, string host)
    {
        if (HostGroups.Allowing(plan.Network.Groups, host) is { } group)
        {
            return $"{host} is allowed through the '{group.Name}' group.\n";
        }

        var learn = plan.Network.Mode == NetworkMode.Learn ? $"While the sandbox is learning{(LearnDate(plan) is { } d ? " until " + d : string.Empty)}, every host on port 80 or 443 is allowed and recorded.\n" : string.Empty;
        return $"{host} is not allowed: no host group lists it.\n{learn}The owner can allow it for this repo: add it to sandbox.repos.<repo>.hosts in the profile.\n";
    }

    private static string ExplainCommand(SandboxPlan plan, string command)
    {
        var rule = plan.Policy.Rules.FirstOrDefault(r => r.Effect == InnerRuleEffect.Deny && string.Equals(r.Tool, "Bash", StringComparison.Ordinal) && Matches(r.Pattern, command));
        return rule is null
            ? $"No rule blocks '{command}'. Whether it works depends on the files and hosts it needs; ask about those.\n"
            : $"'{command}' is blocked in the sandbox (rule: {rule.Pattern}).\nPush and secrets are blocked. Run it in a new terminal window outside the sandbox, or let Sindri send it.\n";
    }

    private static bool LooksLikeHost(string q) =>
        HostShape.IsMatch(q) && !_fileExtensions.Contains(q[(q.LastIndexOf('.') + 1)..].ToLowerInvariant(), StringComparer.Ordinal);

    // Resolves '.', '..', repeated and trailing slashes lexically, so a path can't hide from a rule by how it is written.
    private static string Normalize(string path)
    {
        List<string> kept = [];
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(segment, "..", StringComparison.Ordinal))
            {
                if (kept.Count > 0)
                {
                    kept.RemoveAt(kept.Count - 1);
                }
            }
            else if (!string.Equals(segment, ".", StringComparison.Ordinal))
            {
                kept.Add(segment);
            }
        }

        return "/" + string.Join('/', kept);
    }

    private static bool Matches(string pattern, string command)
    {
        if (pattern.EndsWith(":*", StringComparison.Ordinal))
        {
            var prefix = pattern[..^2];
            return string.Equals(command, prefix, StringComparison.Ordinal) || command.StartsWith(prefix + " ", StringComparison.Ordinal);
        }

        return Regex.IsMatch(command, "^" + Regex.Escape(pattern).Replace("\\*", ".*", StringComparison.Ordinal) + "\\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    }

    [GeneratedRegex("^[A-Za-z0-9-]+(\\.[A-Za-z0-9-]+)+\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex HostShape { get; }
}
