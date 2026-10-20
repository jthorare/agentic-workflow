using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Jobs;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The job calls of the fake (API P6): each answers the API's goldens, the same text the handler serves.</summary>
internal sealed partial class FakeSindriApi
{
    private readonly List<string> _cancelled = [];
    private int _jobsCalls;
    private int _jobCalls;

    /// <summary>GET /sindri/jobs's answer: the running golden by default.</summary>
    public RouteAnswer JobsAnswer { get; set; } = RouteAnswer.Api("jobs", "running");

    /// <summary>GET /sindri/jobs/{id}'s answer per job id; an id not here answers the SND-JOB-404 golden.</summary>
    public Dictionary<string, RouteAnswer> JobAnswers { get; } = new(StringComparer.Ordinal);

    /// <summary>POST /sindri/jobs/{id}/cancel's answer.</summary>
    public RouteAnswer CancelJobAnswer { get; set; } = RouteAnswer.Api("jobs-id-cancel", "accepted");

    /// <summary>How many times the job list was read.</summary>
    public int JobsCalls => Volatile.Read(ref _jobsCalls);

    /// <summary>How many times one job was read.</summary>
    public int JobCalls => Volatile.Read(ref _jobCalls);

    /// <summary>The query of the latest list read; null before the first.</summary>
    public JobQuery? LastJobsQuery { get; private set; }

    /// <summary>The cancellation token the last list read was given.</summary>
    public CancellationToken LastJobsToken { get; private set; }

    /// <summary>Every job a cancel was sent for, in order.</summary>
    public IReadOnlyList<string> CancelledJobs { get { lock (_gate) { return [.. _cancelled]; } } }

    /// <summary>What GET /sindri/jobs/{id} answers.</summary>
    /// <param name="id">The job.</param>
    /// <returns>The answer.</returns>
    internal RouteAnswer JobOneAnswer(string id) => JobAnswers.TryGetValue(id, out var own) ? own : RouteAnswer.Problem("jobs-id", "not-found");

    public Task<Result<JobList>> Jobs(Uri api, JobQuery query, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _jobsCalls);
        LastJobsQuery = query;
        LastJobsToken = cancellationToken;
        return Task.FromResult(Answer<JobList>(api, JobRoutes.List(query), JobsAnswer));
    }

    public Task<Result<JobView>> Job(Uri api, string id, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _jobCalls);
        return Task.FromResult(Answer<JobView>(api, JobRoutes.One(id), JobOneAnswer(id)));
    }

    public async Task<Result<CommandAcceptedView>> CancelJob(Uri api, string id, CancellationToken cancellationToken)
    {
        if (NotReceived<CommandAcceptedView>(api) is { } unanswered)
        {
            return unanswered;
        }

        lock (_gate)
        {
            _cancelled.Add(id);
        }

        await WaitFor(CommandGate, cancellationToken).ConfigureAwait(false);
        var answer = Answer<CommandAcceptedView>(api, JobRoutes.Cancel(id), CancelJobAnswer);
        if (answer.IsSuccess)
        {
            _ = Interlocked.Exchange(ref _pending, answer.Value.Sequence);
        }

        return answer;
    }
}
