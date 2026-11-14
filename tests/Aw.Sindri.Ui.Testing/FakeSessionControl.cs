using Aw.Sindri.Ui.Terminal;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The in-memory session control: which sessions were stopped or interrupted, in order, and a refusal when the test says so.</summary>
internal sealed class FakeSessionControl : ISessionControl
{
    private readonly List<string> _stopped = [];
    private readonly List<string> _interrupted = [];

    /// <summary>Refs stopped, in order.</summary>
    public IReadOnlyList<string> Stopped => _stopped;

    /// <summary>Refs interrupted, in order.</summary>
    public IReadOnlyList<string> Interrupted => _interrupted;

    /// <summary>When true, every call answers false.</summary>
    public bool Refuse { get; set; }

    public Task<bool> Stop(string sessionRef, CancellationToken cancellationToken)
    {
        if (!Refuse)
        {
            _stopped.Add(sessionRef);
        }

        return Task.FromResult(!Refuse);
    }

    public Task<bool> Interrupt(string sessionRef, CancellationToken cancellationToken)
    {
        if (!Refuse)
        {
            _interrupted.Add(sessionRef);
        }

        return Task.FromResult(!Refuse);
    }
}
