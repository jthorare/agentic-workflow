using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Tests.ListSessions;

/// <summary>An <see cref="ISessionSandboxes"/> that answers what a test set and remembers which launches it was asked about.</summary>
internal sealed class FakeSessionSandboxes : ISessionSandboxes
{
    private readonly Dictionary<LaunchId, SessionSandbox> _answers = [];
    private readonly List<LaunchId> _asked = [];

    /// <summary>The launches read so far, in order.</summary>
    public IReadOnlyList<LaunchId> Asked => _asked;

    /// <summary>Sets the answer for a launch.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="sandbox">What it answers.</param>
    public void Answer(LaunchId launch, SessionSandbox sandbox) => _answers[launch] = sandbox;

    public ValueTask<SessionSandbox> Read(LaunchId launch, SandboxSummary summary, CancellationToken cancellationToken)
    {
        _asked.Add(launch);
        return ValueTask.FromResult(_answers.TryGetValue(launch, out var found) ? found : new SessionSandbox(SessionSandboxState.Unavailable, summary.Attended, Folder: null, Memory: null));
    }
}
