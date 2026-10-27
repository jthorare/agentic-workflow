using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The plan hash: SHA-256 over a canonical text of the plan, first 12 hex digits. Per-launch paths are replaced by placeholders, so the same policy hashes the same across launches.</summary>
public static class PlanHasher
{
    private const int Length = 12;

    // Variables whose value differs on every launch (or carries a port); the paths in the others are normalized like the mounts.
    private static readonly string[] _perLaunchEnvironment =
        ["AW_SINDRI_LAUNCH", "AW_SINDRI_ITEM", "AW_SINDRI_INSTANCE", "HTTPS_PROXY", "HTTP_PROXY", "ALL_PROXY", "https_proxy", "http_proxy", "all_proxy", "TERM", "LANG", "USER", "SHELL"];

    /// <summary>Hashes a plan.</summary>
    /// <param name="plan">The plan (its own PlanHash is ignored).</param>
    /// <param name="perLaunchPaths">Placeholder and path pairs ('$WORKTREE', '/work/x'), replaced longest path first.</param>
    /// <returns>12 lower-case hex digits.</returns>
    public static string Hash(SandboxPlan plan, IReadOnlyList<(string Placeholder, string Path)> perLaunchPaths)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(perLaunchPaths);
        var ordered = perLaunchPaths.Where(p => p.Path.Length > 0).OrderByDescending(p => p.Path.Length).ToList();
        string Norm(string? text) => ordered.Aggregate(text ?? string.Empty, (current, p) => current.Replace(p.Path, p.Placeholder, StringComparison.Ordinal));
        var text = Canonical(plan, Norm);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..Length];
    }

    private static string Canonical(SandboxPlan plan, Func<string?, string> norm)
    {
        var inv = CultureInfo.InvariantCulture;
        var l = plan.Limits;
        List<string> lines =
        [
            string.Create(inv, $"v{plan.PlanVersion} {plan.Mechanism} attended={plan.Attended}"),
            $"login={plan.Login} github={plan.Github} inner={plan.Inner} net={plan.Network.Mode}",
            string.Create(inv, $"limits {l.MemoryMaxBytes} {l.MemoryHighBytes} {l.CpuQuotaPercent} {l.TasksMax} {l.Sessions} {l.TotalMemoryBytes} {l.HomeTmpfsBytes} {l.WallGrace} {l.ParkMax} {l.Wall} {l.RuntimeMax}"),
        ];
        lines.AddRange(plan.Network.Groups.Select(g => $"group {g.Name}={string.Join(',', g.Hosts)}"));
        lines.AddRange(plan.Mounts.Select(m => string.Create(inv, $"mount {m.Kind} {norm(m.Path)} <- {norm(m.Source)} {m.Size}")));
        lines.AddRange(plan.Environment.Select(e => _perLaunchEnvironment.Contains(e.Name, StringComparer.Ordinal) || e.Value is null ? "env " + e.Name : $"env {e.Name}={norm(e.Value)}"));
        lines.AddRange(plan.Git.Select(g => $"git {g.Key}={norm(g.Value)}"));
        lines.Add("policy " + plan.Policy.Mode);
        lines.AddRange(plan.Policy.Rules.Select(r => $"rule {r.Effect} {r.Tool} {norm(r.Pattern)}"));
        lines.Add("hooks " + string.Join(',', plan.Hooks.Kept) + " | " + string.Join(',', plan.Hooks.Omitted));
        return string.Join('\n', lines);
    }
}
