using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Testing;

/// <summary>A readiness probe that answers a scripted probe per call. Held to <see cref="OnyxHealthContract"/>.</summary>
public sealed class FakeOnyxHealth : IOnyxHealth
{
    private readonly Queue<OnyxProbe> _answers = new();

    /// <summary>Gets or sets the probe answered when no queued one is left.</summary>
    public OnyxProbe Default { get; set; } = new(Healthy: false, BackendVersion: null);

    /// <summary>Gets the ports probed, in order.</summary>
    public IList<int> Probed { get; } = [];

    /// <summary>Gets the origins probed, in order.</summary>
    public IList<OnyxOrigin> ProbedOrigins { get; } = [];

    /// <summary>Queues probes the next calls answer, one each.</summary>
    /// <param name="probes">The probes.</param>
    public void Enqueue(params OnyxProbe[] probes)
    {
        foreach (var probe in probes)
        {
            _answers.Enqueue(probe);
        }
    }

    /// <inheritdoc />
    public ValueTask<OnyxProbe> Probe(OnyxOrigin origin, CancellationToken cancellationToken)
    {
        Probed.Add(origin.Port);
        ProbedOrigins.Add(origin);
        return ValueTask.FromResult(_answers.Count > 0 ? _answers.Dequeue() : Default);
    }
}
