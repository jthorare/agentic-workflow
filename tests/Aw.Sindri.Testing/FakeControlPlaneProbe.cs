using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Testing;

/// <summary>The control-plane probe with a canned answer (unreachable until <see cref="Answer"/> is set).</summary>
public sealed class FakeControlPlaneProbe : IControlPlaneProbe
{
    /// <summary>The answer the probe gives.</summary>
    public Result<(ControlEndpoint Endpoint, Ping Ping)> Answer { get; set; } = SindriErrors.NotRunning.With("no control plane is recorded");

    /// <summary>
    /// A probe over a recorded endpoint and what it answers, by the port's rule: found only when the answer carries the
    /// recorded pid and epoch.
    /// </summary>
    /// <param name="recorded">The endpoint record, or null.</param>
    /// <param name="answer">The ping the endpoint answers.</param>
    /// <returns>The probe.</returns>
    public static FakeControlPlaneProbe For(ControlEndpoint? recorded, Ping answer) => new()
    {
        Answer = recorded switch
        {
            null => SindriErrors.NotRunning.With("no control plane is recorded"),
            _ when answer.Pid == recorded.Pid && answer.Epoch == recorded.Epoch => (recorded, answer),
            _ => SindriErrors.NotRunning.With($"{recorded.Url.Authority} answered, but not as the recorded control plane"),
        },
    };

    /// <inheritdoc/>
    public ValueTask<Result<(ControlEndpoint Endpoint, Ping Ping)>> Check(CancellationToken cancellationToken) => ValueTask.FromResult(Answer);
}
