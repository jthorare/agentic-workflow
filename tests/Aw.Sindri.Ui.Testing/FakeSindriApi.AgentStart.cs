using Aw.Kernel;
using Aw.Sindri.Ui.Api.Sessions;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The New agent form's calls of the fake (agents spec §15.4): every start recorded, answered from a queue or with a Starting session.</summary>
internal sealed partial class FakeSindriApi
{
    private readonly Queue<Result<AgentStartedView>> _startAnswers = new();
    private readonly List<StartAgentRequest> _starts = [];

    /// <summary>What GET /sessions/last-start answers.</summary>
    public AgentLastStartView LastStartAnswer { get; set; } = new(Repo: null, Agent: null);

    /// <summary>Every start body received, in order.</summary>
    public IReadOnlyList<StartAgentRequest> Starts { get { lock (_gate) { return [.. _starts]; } } }

    /// <summary>Answers the next start with this instead of a Starting session.</summary>
    /// <param name="answer">The answer.</param>
    public void QueueStart(Result<AgentStartedView> answer)
    {
        lock (_gate)
        {
            _startAnswers.Enqueue(answer);
        }
    }

    public Task<Result<AgentStartedView>> Start(Uri api, StartAgentRequest request, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _starts.Add(request);
            return Task.FromResult(_startAnswers.Count > 0
                ? _startAnswers.Dequeue()
                : new AgentStartedView("sindri-" + request.ActId, request.ActId, "starting", new AgentStartedHost("/tmp/tmux-1/sindri-launch", "sindri-" + request.ActId, "%1")));
        }
    }

    public Task<Result<AgentLastStartView>> LastStart(Uri api, CancellationToken cancellationToken) => Task.FromResult<Result<AgentLastStartView>>(LastStartAnswer);
}
