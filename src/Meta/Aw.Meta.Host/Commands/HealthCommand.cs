using System.Globalization;
using Aw.Meta.Core.Health;
using Aw.Meta.Features.Health;

namespace Aw.Meta.Host.Commands;

/// <summary>The 'health' command: exit 1 when an orphan is still running, a lane is missing or memory is low. Works without a team config (the lane check is then skipped).</summary>
internal static class HealthCommand
{
    internal static CommandEntry Entry { get; } = new(
        "health",
        new CommandSpec("health [--kill-orphans] [--json]", [], [], ["kill-orphans"]),
        NeedsConfig: false,
        static context => CommandOutput.Call<HealthRequest, HealthResponse>(context, new HealthRequest(context.Line.Has("kill-orphans")), Text, static r => r.Attention));

    private static string Text(HealthResponse health) =>
        string.Join(
            Environment.NewLine,
            [
                string.Create(CultureInfo.InvariantCulture, $"processes: {health.ProcessCount}"),
                .. health.Groups.Select(static g => string.Create(CultureInfo.InvariantCulture, $"  {g.Name,-22} {g.Count,4}  {g.RssMb,7} MB")),
                Memory(health),
                .. Orphans(health),
                .. health.Duplicates.Select(static d => string.Create(CultureInfo.InvariantCulture, $"duplicate servers: {d.Copies} copies (expected at most {d.Allowed}): {d.Cmdline}")),
                health.MissingLanes switch
                {
                    null => "lanes: not checked (no team config)",
                    { Count: 0 } => "lanes: all have a live session",
                    var missing => "lanes with no live session: " + string.Join(", ", missing),
                },
                .. (health.Lanes ?? []).Select(static l => $"lane {l.Lane} ({l.Agent}): {l.State}"),
                .. ContextLine.Of(health.Context),
                .. health.Consumer is { } consumer ? [$"consumer: {consumer}"] : Array.Empty<string>(),
                .. HeavyLocks(health),
                .. Adaptation(health),
                .. health.OomPolicy is { } oom ? [$"oom policy: {oom}"] : Array.Empty<string>(),
                .. health.StaleScripts is { } stale ? [$"stale scripts: {stale}"] : Array.Empty<string>(),
                health.Attention ? "HEALTH ATTENTION" : "HEALTH OK",
            ]);

    private static string Memory(HealthResponse health)
    {
        var host = health.Host;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"memory: {host.MemAvailableKb / 1024} MB available of {host.MemTotalKb / 1024} MB ({host.AvailablePercent}%){(health.MemoryLow ? " LOW" : string.Empty)}; swap used {host.SwapUsedKb / 1024} MB (informational); disk free {host.DiskFreeBytes / (1024 * 1024 * 1024)} GB");
    }

    private static IEnumerable<string> Adaptation(HealthResponse health)
    {
        if (health.Heavy is { } heavy)
        {
            yield return heavy.Line();
        }

        if (health.Builders is { } builders)
        {
            yield return builders.Line();
        }
    }

    private static IEnumerable<string> Orphans(HealthResponse health) =>
        health.Orphans.Count == 0
            ? ["orphans: none"]
            : health.Orphans.Select(o => string.Create(CultureInfo.InvariantCulture, $"orphan pid {o.Pid} (up {o.AgeSeconds / 60} min, {o.RssKb / 1024} MB){(health.Killed.Contains(o.Pid) ? " KILLED" : string.Empty)}: {o.Cmdline}"));

    private static IEnumerable<string> HeavyLocks(HealthResponse health) =>
        health.HeavyLocks.Count == 0
            ? ["heavy lock: free"]
            : health.HeavyLocks.Select(static l => "heavy lock " + l.Path + ": " + Holder(l));

    private static string Holder(HeavyLock slot) =>
        (slot.Pid is { } pid ? string.Create(CultureInfo.InvariantCulture, $"pid {pid} ") + (slot.Alive ? "running" : "gone (stale)") : "holder unknown")
        + (slot.Holder.Length > 0 ? " [" + slot.Holder + "]" : string.Empty);
}
