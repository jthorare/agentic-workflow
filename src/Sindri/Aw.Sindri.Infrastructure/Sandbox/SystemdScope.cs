using System.Globalization;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// The real scope control: 'systemctl --user' for the unit and the cgroup files for the counters (sandbox spec §3.3, spike item 4). The files are readable only while
/// the scope is alive, so the watchdog keeps the last reading; what survives the scope is systemd's 'Result' of the failed unit.
/// </summary>
/// <param name="unit">The scope unit.</param>
/// <param name="processes">Runs systemctl.</param>
/// <param name="host">Reads the cgroup files.</param>
internal sealed class SystemdScope(string unit, ISandboxProcesses processes, IHostPaths host) : IScopeControl
{
    private const string Systemctl = "systemctl";
    private const string CgroupRoot = "/sys/fs/cgroup";
    private static readonly TimeSpan _call = TimeSpan.FromSeconds(5);
    private string? _group;

    /// <inheritdoc />
    public ValueTask<ScopeSample?> Sample(CancellationToken cancellationToken)
    {
        if (ControlGroup() is not { } group || host.ReadText($"{group}/memory.events") is not { } events)
        {
            return ValueTask.FromResult<ScopeSample?>(null);
        }

        var current = Number(host.ReadText($"{group}/memory.current"));
        var peak = host.ReadText($"{group}/memory.peak") is { } text ? Number(text) : current;   // a kernel without memory.peak: the current value is the best we have
        return ValueTask.FromResult<ScopeSample?>(new ScopeSample(current, peak, MemoryEvents.Parse(events), (int)Number(host.ReadText($"{group}/pids.current")), CpuMicroseconds(host.ReadText($"{group}/cpu.stat"))));
    }

    /// <inheritdoc />
    public ValueTask<string?> Result(CancellationToken cancellationToken)
    {
        var shown = Show();
        return ValueTask.FromResult(string.Equals(shown.GetValueOrDefault("ActiveState"), "failed", StringComparison.Ordinal) ? shown.GetValueOrDefault("Result") : null);
    }

    /// <inheritdoc />
    public ValueTask SetRuntimeMax(TimeSpan runtimeMax, CancellationToken cancellationToken)
    {
        _ = processes.Run(Systemctl, SystemdScopeArgv.SetRuntimeMax(unit, runtimeMax), _call);   // the ceiling is a last resort: a refusal here must not stop the session
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask Kill(bool force, CancellationToken cancellationToken)
    {
        _ = processes.Run(Systemctl, SystemdScopeArgv.Kill(unit, force), _call);
        return ValueTask.CompletedTask;
    }

    // The scope's cgroup, as systemd names it ('sindri-steps.slice' nests under 'sindri.slice'), looked up once while the unit is active.
    private string? ControlGroup()
    {
        if (_group is not null)
        {
            return _group;
        }

        var shown = Show();
        if (!string.Equals(shown.GetValueOrDefault("ActiveState"), "active", StringComparison.Ordinal) || shown.GetValueOrDefault("ControlGroup") is not { Length: > 0 } relative)
        {
            return null;
        }

        _group = CgroupRoot + relative;
        return _group;
    }

    private Dictionary<string, string> Show()
    {
        var shown = new Dictionary<string, string>(StringComparer.Ordinal);
        var result = processes.Run(Systemctl, SystemdScopeArgv.Show(unit), _call);
        if (!result.Succeeded)
        {
            return shown;
        }

        foreach (var line in result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var split = line.IndexOf('=', StringComparison.Ordinal);
            if (split > 0)
            {
                shown[line[..split]] = line[(split + 1)..];
            }
        }

        return shown;
    }

    private static long Number(string? text) => long.TryParse(text?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static long CpuMicroseconds(string? stat) =>
        (stat ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(p => p is ["usage_usec", _])
            .Select(p => Number(p[1]))
            .FirstOrDefault();
}
