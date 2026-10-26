using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>Issue 1041: a map built under older rules is refreshed although no head moved (the refresh tick and its pure decision).</summary>
public sealed class MapRulesRefreshTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
    private readonly SliceHarness _h = new();
    private readonly Starter _start;
    private readonly HeadsEngineProxy _engine = HeadsEngineProxy.Create();
    private Core.Lease.LeaseGrant? _grant;

    public MapRulesRefreshTests()
    {
        _start = new Starter(_h);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Dictionary<string, string> Heads(params (string Repo, string Sha)[] heads) => heads.ToDictionary(static h => h.Repo, static h => h.Sha, StringComparer.Ordinal);

    private async Task<Result<ScheduledResult>> Tick(string builtUnder, string running)
    {
        _h.Clock.SetUtcNow(_now);
        var loaded = SliceHarness.Profile("5s");
        loaded = loaded with { Profile = loaded.Profile with { Context = JsonDocument.Parse("""{"docs":{"globs":["docs/**/*.md"]}}""").RootElement.Clone() } };
        _ = await _h.Profiles.Save(loaded, Ct);
        _engine.Heads = [new RepoHead(ContextRepoName.From("a"), "1", OnDefault: true)];
        _engine.State = new MapPublishState(Hold: null, LatestDraft: null, Current: 1, [new MapHead(ContextRepoName.From("a"), "1", OnDefault: true)]) { CurrentRuleset = builtUnder };
        var service = new ContextMapRefreshService(new ContextScopes(_h.Profiles, _h.Callers), _engine.Proxy, new InMemoryJobViews(), new ScriptedOutlooks(), new MapJobStarts(_start, new PublishStarter(_h)), _h.Clock, new QuietLogger()) { RunningRuleset = running };
        _grant ??= await _h.HoldLease();
        using var caller = _h.Callers.EnterService("context-map-refresh");
        return await service.Run(new ScheduledRun(_grant, _now), Ct);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Rules_changed_since_the_map_was_built_start_a_refresh_without_a_head_move()
    {
        var result = await Tick(builtUnder: "rules-A", running: "rules-B");

        Assert.False(result.Value.Idle);
        _ = Assert.Single(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Unchanged_rules_and_heads_start_nothing()
    {
        var result = await Tick(builtUnder: "rules-A", running: "rules-A");

        Assert.True(result.Value.Idle);
        Assert.Empty(_start.Requests);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_decision_starts_when_rules_changed_and_no_head_moved()
    {
        var heads = Heads(("a", "1"));

        var action = MainMoveDecision.Decide(new MainMoveInput(heads, heads, RulesChanged: true), running: false, [], _now);

        Assert.Equal(MainMoveAction.Start, action);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_decision_does_nothing_when_neither_rules_nor_heads_changed()
    {
        var heads = Heads(("a", "1"));

        var action = MainMoveDecision.Decide(new MainMoveInput(heads, heads, RulesChanged: false), running: false, [], _now);

        Assert.Equal(MainMoveAction.Nothing, action);
    }

    private sealed class QuietLogger : ILogger<ContextMapRefreshService>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }
    }

    private sealed class PublishStarter(SliceHarness harness) : IService<PublishContextMapRequest, JobStartedResponse>
    {
        public ValueTask<Result<JobStartedResponse>> Handle(PublishContextMapRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<JobStartedResponse>>(new JobStartedResponse(new CommandAccepted(harness.Ids.New<CommandId>(), 1), JobId.New(harness.Ids), new JobLinks("/j", "/c", "/e")));
    }

    private sealed class Starter(SliceHarness harness) : IService<RefreshContextMapRequest, JobStartedResponse>
    {
        internal List<RefreshContextMapRequest> Requests { get; } = [];

        public ValueTask<Result<JobStartedResponse>> Handle(RefreshContextMapRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return ValueTask.FromResult<Result<JobStartedResponse>>(new JobStartedResponse(new CommandAccepted(harness.Ids.New<CommandId>(), 1), JobId.New(harness.Ids), new JobLinks("/j", "/c", "/e")));
        }
    }
}
