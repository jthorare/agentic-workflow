using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Fixtures;
using Aw.Context.Tests.QueryContext;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.SourcesStatus;

public sealed class SourcesStatusTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_never_refreshed_is_degraded_with_the_fix()
    {
        var view = await Engines.Over(new FixedSourceCatalog()).Sources(Engines.Human(), CancellationToken.None);

        var row = view.Value.Sources[0];
        Assert.Equal(("repo-docs", "split-panes", SourceState.Degraded), (row.Source.Value, row.Repo?.Value, row.State));
        Assert.Contains("context-docs-refresh", row.Detail, StringComparison.Ordinal);
        Assert.True(view.Value.SessionsEnabled);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task After_a_refresh_each_repo_reports_its_counts_and_a_failed_one_its_code()
    {
        using var repo = FixtureRepo.Build();
        var index = new FakeDocIndex(new FakeTimeProvider());
        var engine = Engines.Over(new FixedSourceCatalog(), index);
        var scope = Engines.Human() with { Repos = [new ContextRepo(ContextRepoName.From("split-panes"), repo.Path)] };

        var report = await engine.Refresh(scope, CancellationToken.None);
        var view = await engine.Sources(scope, CancellationToken.None);
        index.RefreshCode = "SND-CONTEXT-004";
        _ = await engine.Refresh(scope, CancellationToken.None);
        var failed = await engine.Sources(scope, CancellationToken.None);

        Assert.Equal(2, report.Value.Repos.Single().Added);
        Assert.Equal((SourceState.Ok, 2), (view.Value.Sources.Single().State, view.Value.Sources.Single().Records));
        Assert.Equal((SourceState.Unavailable, "SND-CONTEXT-004"), (failed.Value.Sources.Single().State, failed.Value.Sources.Single().Code));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task After_the_patterns_change_the_repo_is_degraded_until_a_refresh_cleans_it_again()
    {
        const string acme = """{"scrub":{"extraPatterns":[{"id":"acme-customer-id","pattern":"ACME-[0-9]{6}"}]}}""";
        using var repo = FixtureRepo.Build();
        var engine = Engines.Over(new FixedSourceCatalog(), new FakeDocIndex(new FakeTimeProvider()));
        var repos = new[] { new ContextRepo(ContextRepoName.From("split-panes"), repo.Path) };

        _ = await engine.Refresh(Engines.Human() with { Repos = repos }, CancellationToken.None);
        var changed = await engine.Sources(Engines.Human(acme) with { Repos = repos }, CancellationToken.None);
        var report = await engine.Refresh(Engines.Human(acme) with { Repos = repos }, CancellationToken.None);
        var cleaned = await engine.Sources(Engines.Human(acme) with { Repos = repos }, CancellationToken.None);

        Assert.Equal(SourceState.Degraded, changed.Value.Sources.Single().State);
        Assert.StartsWith("2 of 2 documents wait for the next refresh", changed.Value.Sources.Single().Detail, StringComparison.Ordinal);
        Assert.Equal(2, report.Value.Repos.Single().Recleaned);
        Assert.Equal(SourceState.Ok, cleaned.Value.Sources.Single().State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Live_sources_report_their_probe_and_sessions_are_gated()
    {
        var onyx = new FakeContextSource(new SourceDescriptor(SourceId.From("onyx"), SourceKind.Intent, SourceFreshness.Live, SupportsAsOf: false), [])
        {
            Failure = ContextErrors.SourceUnavailable.With("down"),
        };
        var engine = Engines.Over(new FixedSourceCatalog(onyx));

        var view = await engine.Sources(Engines.Human(), CancellationToken.None);
        var session = await engine.Sources(Engines.Session("split-panes"), CancellationToken.None);

        Assert.Equal((SourceState.Unavailable, "SND-CONTEXT-010"), (view.Value.Sources[^1].State, view.Value.Sources[^1].Code));
        Assert.True(session.IsSuccess);
    }

    // Health is not content: doctor already shows a session the onyx row and points it at `sources status` as the fix, so the status lists it too.
    [Fact, Trait("Lane", "unit")]
    public async Task A_session_sees_the_onyx_row_with_its_probe()
    {
        var (session, _) = await OnyxRows();

        Assert.Equal((SourceState.Unavailable, "SND-CONTEXT-010"), (session.State, session.Code));
    }

    // A session searches the intent sources like a human (owner q93, 2026-10-23), so its row carries the sync counts too.
    [Fact, Trait("Lane", "unit")]
    public async Task A_sessions_onyx_row_says_nothing_of_a_refusal_and_carries_the_sync_counts()
    {
        var (session, human) = await OnyxRows();

        Assert.DoesNotContain("sessions don't search it", session.Detail, StringComparison.Ordinal);
        Assert.NotNull(session.Sync);
        Assert.NotNull(human.Sync);
    }

    private static async Task<(SourceStatusRow Session, SourceStatusRow Human)> OnyxRows()
    {
        const string on = """{"sessions":{"enabled":true}}""";
        var onyx = new FakeContextSource(new SourceDescriptor(SourceId.From("onyx"), SourceKind.Intent, SourceFreshness.Live, SupportsAsOf: false), [])
        {
            Failure = ContextErrors.SourceUnavailable.With("down"),
        };
        var engine = Engines.Over(new FixedSourceCatalog(onyx));

        var session = await engine.Sources(Engines.Session("split-panes", on), CancellationToken.None);
        var human = await engine.Sources(Engines.Human(on), CancellationToken.None);

        return (Assert.Single(session.Value.Sources, static r => r.Source.Value == "onyx"), Assert.Single(human.Value.Sources, static r => r.Source.Value == "onyx"));
    }
}
