using Aw.Sindri.Core.Lease;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>The contract every <see cref="IProcessProbe"/> honours, run against the fake and the POSIX adapter.</summary>
public abstract class ProcessProbeContract
{
    /// <summary>The probe under test.</summary>
    /// <returns>The probe.</returns>
    protected abstract IProcessProbe Create();

    /// <summary>A pid with no running process behind it.</summary>
    /// <returns>The pid.</returns>
    protected abstract ValueTask<int> GonePid();

    /// <summary>A running pid, and a start time an earlier process with that pid had.</summary>
    /// <returns>The pid and the earlier start.</returns>
    protected abstract (int Pid, DateTimeOffset EarlierStart) ReusedPid();

    /// <summary>This process runs, and its facts match its holder record.</summary>
    [Fact]
    public void This_process_is_running_with_its_own_start_time()
    {
        var probe = Create();
        var self = probe.Self();

        var facts = probe.Facts(self.Pid);

        Expect.True(facts.Running);
        Expect.Equal(HolderLiveness.Alive, LeaseDecision.Liveness(self, self, facts));
        Expect.True(!string.IsNullOrWhiteSpace(self.BootId.Value));
    }

    /// <summary>A pid with nothing behind it isn't running and has no start time.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_gone_pid_is_not_running_and_has_no_start_time()
    {
        var probe = Create();

        var facts = probe.Facts(await GonePid());

        Expect.Equal(new ProcessFacts(Running: false, StartedAt: null), facts);
    }

    /// <summary>A pid reused by a later process reports the later start time, so the earlier holder reads as dead.</summary>
    [Fact]
    public void A_reused_pid_reports_a_different_start_time()
    {
        var probe = Create();
        var (pid, earlier) = ReusedPid();

        var facts = probe.Facts(pid);

        Expect.True(facts.Running && facts.StartedAt is { } started && started != earlier);
        Expect.Equal(HolderLiveness.ProvenDead, LeaseDecision.Liveness(probe.Self() with { Pid = pid, PidStart = earlier }, probe.Self(), facts));
    }
}
