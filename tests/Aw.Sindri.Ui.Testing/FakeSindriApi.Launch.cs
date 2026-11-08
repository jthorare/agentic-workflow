using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The agent run's calls (<see cref="FakeSindriApi"/>, continued): a launch, the Attach send and an instance's events.</summary>
internal sealed partial class FakeSindriApi
{
    private readonly List<(string Item, OpenItemRequest Request)> _opened = [];
    private int _launchCalls;

    /// <summary>GET /sindri/launches/{id}'s answer per launch id; an id not here answers the SND-LAUNCH-404 golden.</summary>
    public Dictionary<string, RouteAnswer> Launches { get; } = new(StringComparer.Ordinal);

    /// <summary>POST /sindri/items/{id}/open's answer.</summary>
    public RouteAnswer Open { get; set; } = RouteAnswer.Api("items-id-open", "requested");

    /// <summary>Every OpenItem received (item, body), in order.</summary>
    public IReadOnlyList<(string Item, OpenItemRequest Request)> Opened { get { lock (_gate) { return [.. _opened]; } } }

    /// <summary>How many launches were read.</summary>
    public int LaunchCalls => Volatile.Read(ref _launchCalls);

    public Task<Result<LaunchView>> Launch(Uri api, string launch, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _launchCalls);
        return Task.FromResult(Answer<LaunchView>(api, "/sindri/launches/{id}", Launches.TryGetValue(launch, out var own) ? own : RouteAnswer.Problem("launches-id", "not-found")));
    }

    public async Task<Result<OpenAcceptedView>> OpenItem(Uri api, string itemId, OpenItemRequest request, CancellationToken cancellationToken)
    {
        if (NotReceived<OpenAcceptedView>(api) is { } unanswered)
        {
            return unanswered;
        }

        lock (_gate)
        {
            _opened.Add((itemId, request));
        }

        await WaitFor(CommandGate, cancellationToken).ConfigureAwait(false);
        var answer = Answer<OpenAcceptedView>(api, "/sindri/items/{id}/open", Open);
        if (answer.IsSuccess)
        {
            _ = Interlocked.Exchange(ref _pending, answer.Value.Accepted.Sequence);
        }

        return answer;
    }
}
