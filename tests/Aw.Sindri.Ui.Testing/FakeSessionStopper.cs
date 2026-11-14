using Aw.Sindri.Ui.Terminal;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The in-memory stop route: POST /sindri/sessions/{provider}/{id}/stop. Records provider/id and answers true, or false when the test says so.</summary>
internal sealed class FakeSessionStopper : ISessionStopper
{
    private readonly List<string> _stopped = [];

    /// <summary>Sessions stopped, provider/id, in order.</summary>
    public IReadOnlyList<string> Stopped => _stopped;

    /// <summary>When true, every stop is refused.</summary>
    public bool Refuse { get; set; }

    public Task<int> Stop(Uri api, string provider, string id, CancellationToken cancellationToken)
    {
        if (!Refuse)
        {
            _stopped.Add(provider + "/" + id);
        }

        return Task.FromResult(Refuse ? 404 : 200);
    }
}
