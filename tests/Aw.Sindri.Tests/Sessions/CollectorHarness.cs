using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Features.Sessions;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The collector's ports in memory with the real services over them: one pass is <see cref="Pass"/>.</summary>
internal sealed class CollectorHarness
{
    public CollectorHarness(SessionSettings? settings = null, IProcessTable? processes = null, ITextScrubber? scrubber = null, IClaudeSessionRecords? records = null, IFleetStates? fleet = null, ILogger<CollectSessionsService>? logger = null, SessionHosts? hosts = null)
    {
        Settings = settings ?? SessionSettings.Defaults;
        Processes = processes ?? Fake;
        var catalog = new AgentCatalog(SessionData.Agents);
        Discovery = new SessionDiscovery(Processes, records ?? Records, fleet ?? Fleet, catalog, Settings, hosts ?? SessionHostData.None(Processes), Base.Clock);
        var repos = new SessionRepos(Locator, Base.Profiles, Base.Clock);
        Observer = new SessionObserver(Transcripts, scrubber ?? new PassThroughScrubber(), repos);
        Service = new CollectSessionsService(Discovery, Observer, Memory, Snapshot, Base.Clock, logger ?? NullLogger<CollectSessionsService>.Instance);
    }

    public SliceHarness Base { get; } = new();

    public SessionSettings Settings { get; }

    public FakeProcessTable Fake { get; } = new();

    public IProcessTable Processes { get; }

    public FakeClaudeSessionRecords Records { get; } = new();

    public FakeFleetStates Fleet { get; } = new();

    public FakeTranscripts Transcripts { get; } = new();

    public FakeRepoLocator Locator { get; } = new();

    public InMemorySessionSnapshot Snapshot { get; } = new();

    public CollectorMemory Memory { get; } = new();

    public SessionDiscovery Discovery { get; }

    public SessionObserver Observer { get; }

    public CollectSessionsService Service { get; }

    public DateTimeOffset Now => Base.Clock.GetUtcNow();

    /// <summary>Runs one pass with a generous budget and returns its response.</summary>
    public async Task<CollectSessionsResponse> Pass(TimeSpan? budget = null)
    {
        var result = await Service.Handle(new CollectSessionsRequest(budget ?? TimeSpan.FromSeconds(30)), TestContext.Current.CancellationToken);
        return result.Value;
    }

    /// <summary>Steps the clock by <paramref name="by"/>, then runs a pass.</summary>
    public async Task<CollectSessionsResponse> After(TimeSpan by)
    {
        Base.Clock.Advance(by);
        return await Pass();
    }
}
