using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Api.Jobs;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The context jobs of the fake: the map refresh's start answers the spec-shaped goldens (POST /sindri/context/map/refresh; the API has none), the same text the handler serves.</summary>
internal sealed partial class FakeSindriApi
{
    private int _refreshMapCalls;
    private int _buildMapCalls;
    private readonly List<PinConceptsBody> _pinned = [];
    private readonly List<PagesSetBody> _pagesSet = [];

    /// <summary>POST /sindri/context/map/refresh's answer: the started job by default.</summary>
    public RouteAnswer RefreshContextMapAnswer { get; set; } = RouteAnswer.SpecShaped("context-map-refresh", "started");

    /// <summary>How many times the map refresh was started.</summary>
    public int RefreshContextMapCalls => Volatile.Read(ref _refreshMapCalls);

    public Task<Result<JobStarted>> RefreshContextMap(Uri api, CancellationToken cancellationToken)
    {
        if (NotReceived<JobStarted>(api) is { } unanswered)
        {
            return Task.FromResult(unanswered);
        }

        _ = Interlocked.Increment(ref _refreshMapCalls);
        return Task.FromResult(Answer<JobStarted>(api, JobRoutes.RefreshContextMap, RefreshContextMapAnswer));
    }

    /// <summary>POST /sindri/context/map/build's answer: the started job by default (the refresh's spec-shaped golden; the API's start answer has the same shape).</summary>
    public RouteAnswer BuildContextMapAnswer { get; set; } = RouteAnswer.SpecShaped("context-map-refresh", "started");

    /// <summary>How many times the map build was started.</summary>
    public int BuildContextMapCalls => Volatile.Read(ref _buildMapCalls);

    public Task<Result<JobStarted>> BuildContextMap(Uri api, CancellationToken cancellationToken)
    {
        if (NotReceived<JobStarted>(api) is { } unanswered)
        {
            return Task.FromResult(unanswered);
        }

        _ = Interlocked.Increment(ref _buildMapCalls);
        return Task.FromResult(Answer<JobStarted>(api, JobRoutes.BuildContextMap, BuildContextMapAnswer));
    }

    /// <summary>POST /sindri/context/pins' answer: the started job by default (the refresh's spec-shaped golden; the API's start answer has the same shape).</summary>
    public RouteAnswer PinConceptsAnswer { get; set; } = RouteAnswer.SpecShaped("context-map-refresh", "started");

    /// <summary>Every PinConcepts body received, in order.</summary>
    public IReadOnlyList<PinConceptsBody> PinnedConcepts { get { lock (_gate) { return [.. _pinned]; } } }

    public Task<Result<JobStarted>> PinConcepts(Uri api, PinConceptsBody request, CancellationToken cancellationToken)
    {
        if (NotReceived<JobStarted>(api) is { } unanswered)
        {
            return Task.FromResult(unanswered);
        }

        Keep(_pinned, request);
        return Task.FromResult(Answer<JobStarted>(api, JobRoutes.PinConcepts, PinConceptsAnswer));
    }

    /// <summary>POST /sindri/context/pages' answer: the started job by default.</summary>
    public RouteAnswer SetContextPagesAnswer { get; set; } = RouteAnswer.SpecShaped("context-map-refresh", "started");

    /// <summary>Every SetPages body received, in order.</summary>
    public IReadOnlyList<PagesSetBody> PagesSet { get { lock (_gate) { return [.. _pagesSet]; } } }

    public Task<Result<JobStarted>> SetContextPages(Uri api, PagesSetBody request, CancellationToken cancellationToken)
    {
        if (NotReceived<JobStarted>(api) is { } unanswered)
        {
            return Task.FromResult(unanswered);
        }

        Keep(_pagesSet, request);
        return Task.FromResult(Answer<JobStarted>(api, JobRoutes.SetContextPages, SetContextPagesAnswer));
    }
}
