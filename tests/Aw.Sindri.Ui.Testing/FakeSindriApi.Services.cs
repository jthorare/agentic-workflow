using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The service and stop commands of the fake: each answers the API's goldens, the same text the handler serves, and records the service it was sent for.</summary>
internal sealed partial class FakeSindriApi
{
    private readonly List<string> _ran = [];
    private readonly List<string> _paused = [];
    private readonly List<string> _resumed = [];
    private int _stopCalls;

    /// <summary>POST /sindri/services/{name}/run's answer: the started job by default.</summary>
    public RouteAnswer RunServiceAnswer { get; set; } = RouteAnswer.Api("services-name-run", "started");

    /// <summary>POST /sindri/services/{name}/pause's answer.</summary>
    public RouteAnswer PauseServiceAnswer { get; set; } = RouteAnswer.Api("services-name-pause", "accepted");

    /// <summary>POST /sindri/services/{name}/resume's answer.</summary>
    public RouteAnswer ResumeServiceAnswer { get; set; } = RouteAnswer.Api("services-name-resume", "accepted");

    /// <summary>POST /sindri/stop's answer.</summary>
    public RouteAnswer StopAnswer { get; set; } = RouteAnswer.Api("stop", "accepted");

    /// <summary>Every service a run was sent for, in order.</summary>
    public IReadOnlyList<string> RunServiceCalls { get { lock (_gate) { return [.. _ran]; } } }

    /// <summary>Every service a pause was sent for, in order.</summary>
    public IReadOnlyList<string> PauseServiceCalls { get { lock (_gate) { return [.. _paused]; } } }

    /// <summary>Every service a resume was sent for, in order.</summary>
    public IReadOnlyList<string> ResumeServiceCalls { get { lock (_gate) { return [.. _resumed]; } } }

    /// <summary>How many stops were sent.</summary>
    public int StopCalls => Volatile.Read(ref _stopCalls);

    public Task<Result<JobStarted>> RunService(Uri api, string name, CancellationToken cancellationToken)
    {
        if (NotReceived<JobStarted>(api) is { } unanswered)
        {
            return Task.FromResult(unanswered);
        }

        lock (_gate)
        {
            _ran.Add(name);
        }

        return Task.FromResult(Answer<JobStarted>(api, ServiceRoutes.Run(name), RunServiceAnswer));
    }

    public async Task<Result<CommandAcceptedView>> PauseService(Uri api, string name, CancellationToken cancellationToken)
    {
        if (NotReceived<CommandAcceptedView>(api) is { } unanswered)
        {
            return unanswered;
        }

        lock (_gate)
        {
            _paused.Add(name);
        }

        await WaitFor(CommandGate, cancellationToken).ConfigureAwait(false);
        return Answer<CommandAcceptedView>(api, ServiceRoutes.Pause(name), PauseServiceAnswer);
    }

    public async Task<Result<CommandAcceptedView>> ResumeService(Uri api, string name, CancellationToken cancellationToken)
    {
        if (NotReceived<CommandAcceptedView>(api) is { } unanswered)
        {
            return unanswered;
        }

        lock (_gate)
        {
            _resumed.Add(name);
        }

        await WaitFor(CommandGate, cancellationToken).ConfigureAwait(false);
        return Answer<CommandAcceptedView>(api, ServiceRoutes.Resume(name), ResumeServiceAnswer);
    }

    public Task<Result<StopAcceptedView>> StopSindri(Uri api, CancellationToken cancellationToken)
    {
        if (NotReceived<StopAcceptedView>(api) is { } unanswered)
        {
            return Task.FromResult(unanswered);
        }

        _ = Interlocked.Increment(ref _stopCalls);
        return Task.FromResult(Answer<StopAcceptedView>(api, ServiceRoutes.Stop, StopAnswer));
    }
}
