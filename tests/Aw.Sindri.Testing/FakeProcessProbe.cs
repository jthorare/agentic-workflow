using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Testing;

/// <summary>An <see cref="IProcessProbe"/> whose processes the test declares: a pid runs only after <see cref="Start"/>.</summary>
public sealed class FakeProcessProbe : IProcessProbe
{
    private readonly Dictionary<int, DateTimeOffset> _running = [];
    private readonly LeaseHolder _self;

    /// <summary>A probe whose own process is <paramref name="pid"/> on host 'box', boot 'boot-1'.</summary>
    /// <param name="pid">This process's pid.</param>
    public FakeProcessProbe(int pid = 100)
    {
        _self = Holder(pid);
        Start(_self);
    }

    /// <summary>A holder on this probe's machine and boot.</summary>
    /// <param name="pid">The pid.</param>
    /// <param name="startedAt">Its start time (default: the known epoch).</param>
    /// <returns>The holder.</returns>
    public static LeaseHolder Holder(int pid, DateTimeOffset? startedAt = null) =>
        new(HostId.From("box"), pid, startedAt ?? KnownInstants.Epoch, BootId.From("boot-1"));

    /// <summary>A holder on another machine, whose liveness this probe can't know.</summary>
    /// <param name="pid">The pid.</param>
    /// <returns>The holder.</returns>
    public static LeaseHolder Remote(int pid) => new(HostId.From("other-box"), pid, KnownInstants.Epoch, BootId.From("boot-9"));

    /// <summary>Marks <paramref name="holder"/>'s pid as running with its start time.</summary>
    /// <param name="holder">The holder.</param>
    public void Start(LeaseHolder holder) => _running[holder.Pid] = holder.PidStart;

    /// <summary>Marks <paramref name="pid"/> as gone.</summary>
    /// <param name="pid">The pid.</param>
    public void Kill(int pid) => _running.Remove(pid);

    /// <inheritdoc/>
    public LeaseHolder Self() => _self;

    /// <inheritdoc/>
    public ProcessFacts Facts(int pid) =>
        _running.TryGetValue(pid, out var started) ? new ProcessFacts(Running: true, started) : new ProcessFacts(Running: false, StartedAt: null);
}
