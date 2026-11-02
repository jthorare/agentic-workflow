using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.ConceptScan;
using Aw.Context.Core.Map;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The work of a concept-scan job (q110 C): the newest map version, the daily budget over the jobs view, and one succeeded outcome for everything the model does.</summary>
public sealed class ScanContextConceptsServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions _web = new(JsonSerializerDefaults.Web);
    private readonly SliceHarness _h = new();
    private readonly InMemoryJobViews _jobs = new();
    private readonly ScanEngineProxy _engine = ScanEngineProxy.Create();
    private int _next;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<ScanContextConceptsService> Service()
    {
        _h.Clock.SetUtcNow(_now);
        var loaded = SliceHarness.Profile("5s");
        loaded = loaded with { Profile = loaded.Profile with { Context = JsonDocument.Parse("""{"docs":{"globs":["docs/**/*.md"]}}""").RootElement.Clone() } };
        _ = await _h.Profiles.Save(loaded, Ct);
        return new ScanContextConceptsService(new ContextScopes(_h.Profiles, _h.Callers), _engine.Proxy, _jobs, _h.Clock);
    }

    // A scan job that ended 'ago' ago, with the outcome its result recorded.
    private void Scan(TimeSpan ago, string outcome = "scanned", JobState state = JobState.Succeeded)
    {
        _next++;
        var result = JsonSerializer.SerializeToElement(new ScanContextConceptsResult(7, OnDraft: true, outcome, Hints: 1, Searches: 2, Reason: null), _web);
        _jobs.Put(new JobRow(JobId.New(_h.Ids), ContextConceptScanService.Kind, JobKey.From("concept-scan"), Item: null, Repo: null, Service: null, state,
            "service:context-concept-scan", _now - ago - TimeSpan.FromSeconds(30), state == JobState.Running ? null : _now - ago, [], Rounds: 0, Summary: null, Code: null, result, CancelledBy: null, Sequence: _next));
    }

    private void Draft(int version) => _engine.State = new MapPublishState(Hold: null, version, Current: version - 1, CurrentHeads: []);

    [Fact, Trait("Lane", "unit")]
    public async Task The_newest_map_is_scanned_and_the_report_becomes_the_result()
    {
        Draft(7);

        var result = await (await Service()).Handle(new ScanContextConceptsRequest(), Ct);

        Assert.Equal(new ScanContextConceptsResult(7, OnDraft: true, "scanned", Hints: 2, Searches: 3, Reason: null), result.Value);
        Assert.Equal(1, _engine.Scans);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_no_map_at_all_nothing_is_scanned_and_the_job_still_succeeds()
    {
        var result = await (await Service()).Handle(new ScanContextConceptsRequest(), Ct);

        Assert.Equal("no-map", result.Value.Outcome);
        Assert.Equal(0, _engine.Scans);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(ConceptScanOutcome.ModelUnavailable, "model-unavailable")]
    [InlineData(ConceptScanOutcome.NoConcepts, "no-concepts")]
    [InlineData(ConceptScanOutcome.NothingToScan, "nothing-to-scan")]
    public async Task A_model_that_does_not_help_ends_the_job_succeeded_with_that_outcome(ConceptScanOutcome outcome, string expected)
    {
        Draft(7);
        _engine.Report = new ConceptScanReport(7, OnDraft: true, outcome, Hints: 0, Searches: 0);

        var result = await (await Service()).Handle(new ScanContextConceptsRequest(), Ct);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value.Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_seventh_scan_in_a_day_ends_over_budget_with_the_reason_and_asks_no_model()
    {
        Draft(7);
        for (var i = 0; i < ConceptScanLimits.MaxScansPerDay; i++)
        {
            Scan(TimeSpan.FromHours(i + 1));
        }

        var result = await (await Service()).Handle(new ScanContextConceptsRequest(), Ct);

        Assert.Equal("over-budget", result.Value.Outcome);
        Assert.Equal(7, result.Value.Version);
        Assert.Contains("limit is 6", result.Value.Reason, StringComparison.Ordinal);
        Assert.Equal(0, _engine.Scans);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Scans_that_used_no_model_and_scans_older_than_a_day_do_not_count()
    {
        Draft(7);
        for (var i = 0; i < 4; i++)
        {
            Scan(TimeSpan.FromHours(i + 1), outcome: "over-budget");
            Scan(TimeSpan.FromHours(30 + i));
        }

        Scan(TimeSpan.FromMinutes(5), state: JobState.Running);   // the job being served now
        Scan(TimeSpan.FromHours(2));
        Scan(TimeSpan.FromHours(3));

        var result = await (await Service()).Handle(new ScanContextConceptsRequest(), Ct);

        Assert.Equal("scanned", result.Value.Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_engine_error_fails_the_job()
    {
        Draft(7);
        _engine.Report = ContextErrors.StoreUnavailable.With("the store is gone");

        var result = await (await Service()).Handle(new ScanContextConceptsRequest(), Ct);

        Assert.True(result.IsError);
        Assert.Equal(ContextErrors.StoreUnavailable.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_a_current_map_and_no_draft_the_current_version_is_what_the_result_names()
    {
        _engine.State = new MapPublishState(Hold: null, LatestDraft: null, Current: 4, CurrentHeads: []);
        _engine.Report = new ConceptScanReport(4, OnDraft: false, ConceptScanOutcome.Scanned, Hints: 1, Searches: 1);

        var result = await (await Service()).Handle(new ScanContextConceptsRequest(), Ct);

        Assert.Equal(new ScanContextConceptsResult(4, OnDraft: false, "scanned", Hints: 1, Searches: 1, Reason: null), result.Value);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("scanned", "map v7: 1 concept hint(s) from 2 search(es)")]
    [InlineData("no-concepts", "map v7: no concepts")]
    [InlineData("model-unavailable", "map v7: model unavailable")]
    [InlineData("no-map", "no map to scan")]
    public void The_job_summary_says_what_happened_in_one_line(string outcome, string expected) =>
        Assert.Equal(expected, ScanContextConceptsService.Summary(new ScanContextConceptsResult(7, OnDraft: true, outcome, Hints: 1, Searches: 2, Reason: null)));

    [Theory, Trait("Lane", "unit")]
    [InlineData("cheap", null, "map v7: 1 concept hint(s) from 2 search(es) (cheap model)")]
    [InlineData("strong", "sonnet", "map v7: 1 concept hint(s) from 2 search(es) (strong model: sonnet)")]
    public void The_job_summary_names_the_tier_and_the_model_when_the_scan_made_a_call(string tier, string? model, string expected) =>
        Assert.Equal(expected, ScanContextConceptsService.Summary(new ScanContextConceptsResult(7, OnDraft: true, "scanned", Hints: 1, Searches: 2, Reason: null, tier, model)));
}
