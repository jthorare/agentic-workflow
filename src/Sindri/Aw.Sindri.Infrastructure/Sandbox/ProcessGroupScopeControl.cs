using System.Globalization;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// IScopeControl over a session that runs as its own process group (macOS, SB-9b; files in <see cref="ProcessGroupFiles"/>). There is no systemd to ask: the
/// group id file is the unit, a signal goes to the whole group, and there is no kernel memory kill, so 'oom-kill' is whatever the watchdog wrote down itself.
/// </summary>
/// <param name="directory">The folder of the group files.</param>
internal sealed class ProcessGroupScopeControl(string directory) : IScopeControl, IMemoryKills
{
    /// <summary>The prefix of the cgroup-path-shaped handle the stats adapter reads back.</summary>
    internal const string Prefix = "pgid:";

    /// <inheritdoc />
    public Task<string?> ControlGroup(string unit, CancellationToken cancellationToken) =>
        Task.FromResult(Live(unit) is { } pgid ? Prefix + pgid.ToString(CultureInfo.InvariantCulture) : null);

    /// <inheritdoc />
    public Task<string?> Result(string unit, CancellationToken cancellationToken) =>
        Task.FromResult(ProcessGroupFiles.MemoryKilled(directory, unit) ? "oom-kill" : null);

    /// <inheritdoc />
    public Task<bool> Kill(string unit, string signal, CancellationToken cancellationToken) =>
        Task.FromResult(ProcessGroupFiles.ReadPgid(directory, unit) is { } pgid && ProcessGroupFiles.Signal(pgid, signal));

    /// <inheritdoc />
    /// <remarks>Does nothing and says yes: there is no RuntimeMaxSec without systemd, and the watchdog's own wall clock already ends the session.</remarks>
    public Task<bool> SetRuntimeMax(string unit, TimeSpan ceiling, CancellationToken cancellationToken) => Task.FromResult(true);

    /// <inheritdoc />
    public Task<bool> Record(string unit, CancellationToken cancellationToken)
    {
        try
        {
            return Task.FromResult(ProcessGroupFiles.RecordMemoryKill(directory, unit));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(false);
        }
    }

    private int? Live(string unit) => ProcessGroupFiles.ReadPgid(directory, unit) is { } pgid && ProcessGroupFiles.Alive(pgid) ? pgid : null;
}
