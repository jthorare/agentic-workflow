using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Testing.Launch;

/// <summary>An <see cref="ITerminalViewer"/> with scripted outcomes: each Open takes the next one queued, then <see cref="ViewerOutcome.Opened"/>.</summary>
/// <param name="kind">The viewer kind.</param>
public sealed class FakeTerminalViewer(string kind = "exec-template") : ITerminalViewer
{
    private readonly Queue<ViewerOutcome> _script = new();
    private readonly List<ViewerRequest> _requests = [];

    /// <inheritdoc/>
    public string Kind { get; } = kind;

    /// <summary>Every request Open received, in order.</summary>
    public IReadOnlyList<ViewerRequest> Requests => _requests;

    /// <summary>Queues the outcome of the next Open that has none queued before it.</summary>
    /// <param name="outcome">The outcome.</param>
    public void Script(ViewerOutcome outcome) => _script.Enqueue(outcome);

    /// <inheritdoc/>
    public ValueTask<ViewerOutcome> Open(ViewerRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _requests.Add(request);
        return ValueTask.FromResult(_script.TryDequeue(out var next) ? next : ViewerOutcome.Opened);
    }

    /// <inheritdoc/>
    public ValueTask<TerminalFacts> Probe(CancellationToken cancellationToken) =>
        ValueTask.FromResult(new TerminalFacts(Kind, Kind, Present: true, "file", ConfigPath: null, Problem: null, Detail: null));
}
