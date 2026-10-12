using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// The contract every <see cref="IControlPlaneProbe"/> honours: it answers only when the recorded endpoint answers a
/// ping with the recorded pid and epoch. Run against the fake and, over a minimal loopback server, the HTTP adapter.
/// </summary>
public abstract class ControlPlaneProbeContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A probe whose endpoint record holds <paramref name="recorded"/> (pid and epoch; the adapter picks the url), answered by <paramref name="answer"/>.</summary>
    /// <param name="recorded">The recorded pid and epoch, or null for no record.</param>
    /// <param name="answer">What the endpoint answers to a ping.</param>
    /// <returns>The probe.</returns>
    protected abstract ValueTask<IControlPlaneProbe> Create((int Pid, long Epoch)? recorded, Ping answer);

    /// <summary>No record is SND-API-001.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task No_record_is_not_running()
    {
        var probe = await Create(recorded: null, new Ping(42, 3));

        Expect.Equal(SindriErrors.NotRunning.Code, (await probe.Check(Token)).Error.Code);
    }

    /// <summary>A recorded endpoint that answers with its pid and epoch is found.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_recorded_live_endpoint_answers()
    {
        var probe = await Create((42, 3), new Ping(42, 3));

        var found = (await probe.Check(Token)).Value;

        Expect.Equal(new Ping(42, 3), found.Ping);
        Expect.Equal((42, 3L), (found.Endpoint.Pid, found.Endpoint.Epoch));
    }

    /// <summary>An answer with another pid or another epoch isn't the recorded control plane: SND-API-001.</summary>
    /// <param name="pid">The answering pid.</param>
    /// <param name="epoch">The answering epoch.</param>
    /// <returns>The test.</returns>
    [Theory]
    [InlineData(43, 3)]
    [InlineData(42, 4)]
    public async Task A_pid_or_epoch_mismatch_is_not_running(int pid, long epoch)
    {
        var probe = await Create((42, 3), new Ping(pid, epoch));

        Expect.Equal(SindriErrors.NotRunning.Code, (await probe.Check(Token)).Error.Code);
    }
}
