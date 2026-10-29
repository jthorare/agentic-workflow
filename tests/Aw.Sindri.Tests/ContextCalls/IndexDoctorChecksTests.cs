using System.Text.Json;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Testing.Engine;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Secrets;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>Doctor's 'context:index-*' rows for the Sindri-owned record index (Part of #1394): never a value, bounded, ok when not configured.</summary>
public sealed class IndexDoctorChecksTests
{
    private const string AllSources = """{"sources":{"github":{"repos":["owner/name"],"token":"env:GH_KEY"},"linear":{"token":"env:LINEAR_KEY"},"slack":{"channels":[{"id":"C0001GEN","name":"eng"}],"token":"env:SLACK_KEY"}}}""";
    private static readonly PointerSecrets _noSecrets = new(_ => null, new FakeToolRunner(ToolRun.Missing));
    private static readonly PointerSecrets _githubOnly = new(name => name == "GH_KEY" ? "ghp_live-secret-0123" : null, new FakeToolRunner(ToolRun.Missing));
    private static readonly RepoEntry[] _repos = [new RepoEntry("split-panes", "/repos/split-panes")];
    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task No_approved_profile_makes_every_row_ok_not_configured()
    {
        var rows = await Checks(_noSecrets, new FakeProbe(Answer: Unit.Value)).Run(Token);

        Assert.Equal(["context:index-store", "context:index-github", "context:index-linear", "context:index-slack", "context:index-ollama", "context:index-sync"], rows.Select(r => r.Name), StringComparer.Ordinal);
        Assert.All(rows, r => Assert.Equal((CheckState.Ok, "not configured (no profile is approved)", null), (r.State, r.Detail, r.Fix)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_profile_naming_no_record_source_makes_every_row_ok_not_configured_and_never_asks_ollama()
    {
        var probe = new FakeProbe(Answer: Unit.Value);
        await Approve("""{"sources":{}}""");

        var rows = await Checks(_noSecrets, probe).Run(Token);

        Assert.All(rows, r => Assert.Equal(CheckState.Ok, r.State));
        Assert.All(rows, r => Assert.StartsWith("not configured", r.Detail, StringComparison.Ordinal));
        Assert.Equal(0, probe.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Linear_teams_without_a_token_warn_and_point_to_index_connect_linear()
    {
        await Approve("""{"sources":{"linear":{"teams":["ENG"]}}}""");

        var linear = (await Checks(_noSecrets, new FakeProbe(Answer: Unit.Value)).Run(Token)).Single(r => r.Name == "context:index-linear");

        Assert.Equal((CheckState.Warn, "context.sources.linear.token is not set, so linear can't sync", "set context.sources.linear.token to a secret pointer (sindri index connect linear)"), (linear.State, linear.Detail, linear.Fix));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_resolving_pointer_is_ok_and_never_shows_the_token()
    {
        await Approve(AllSources);

        var github = (await Checks(_githubOnly, new FakeProbe(Answer: Unit.Value)).Run(Token)).Single(r => r.Name == "context:index-github");

        Assert.Equal((CheckState.Ok, "env:GH_KEY resolves (not probed: the source is read by sindri index sync)"), (github.State, github.Detail));
        Assert.DoesNotContain("ghp_live-secret-0123", github.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pointer_that_does_not_resolve_fails_with_its_SND_SECRET_reason()
    {
        await Approve(AllSources);

        var rows = await Checks(_githubOnly, new FakeProbe(Answer: Unit.Value)).Run(Token);

        foreach (var name in new[] { "context:index-linear", "context:index-slack" })
        {
            var row = rows.Single(r => r.Name == name);
            Assert.Equal(CheckState.Fail, row.State);
            Assert.StartsWith("SND-SECRET-002:", row.Detail, StringComparison.Ordinal);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_source_the_profile_does_not_name_is_ok_not_configured_beside_one_it_does()
    {
        await Approve("""{"sources":{"github":{"repos":["owner/name"],"token":"env:GH_KEY"}}}""");

        var rows = await Checks(_githubOnly, new FakeProbe(Answer: Unit.Value)).Run(Token);

        Assert.Equal((CheckState.Ok, "not configured (the approved profile names no context.sources.slack)"), (rows.Single(r => r.Name == "context:index-slack").State, rows.Single(r => r.Name == "context:index-slack").Detail));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_record_store_that_does_not_answer_fails_with_the_engine_s_code()
    {
        await Approve(AllSources);

        var store = (await Checks(_githubOnly, new FakeProbe(Answer: Unit.Value)).Run(Token)).Single(r => r.Name == "context:index-store");

        Assert.Equal(CheckState.Fail, store.State);   // why: FakeContextEngine keeps no record store, so Progress is an error
        Assert.StartsWith("the record store doesn't answer: SND-CONTEXT-", store.Detail, StringComparison.Ordinal);
        Assert.Contains("sindri index status", store.Fix, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Ollama_answering_with_the_model_is_ok()
    {
        await Approve(AllSources);

        var ollama = (await Checks(_githubOnly, new FakeProbe(Answer: Unit.Value)).Run(Token)).Single(r => r.Name == "context:index-ollama");

        Assert.Equal(new DoctorCheck("context:index-ollama", CheckState.Ok, "Ollama answers at 127.0.0.1:11434 with nomic-embed-text:latest", Fix: null), ollama);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Ollama_down_or_missing_the_model_warns_with_a_fix_and_is_not_a_fail()
    {
        await Approve(AllSources);

        var ollama = (await Checks(_githubOnly, new FakeProbe(Answer: Core.SindriErrors.EngineUnavailable.With("the embedding server did not answer"))).Run(Token)).Single(r => r.Name == "context:index-ollama");

        Assert.Equal(CheckState.Warn, ollama.State);
        Assert.Contains("did not answer", ollama.Detail, StringComparison.Ordinal);
        Assert.Contains("ollama pull nomic-embed-text:latest", ollama.Fix, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Ollama_that_hangs_warns_after_the_limit_and_doctor_returns()
    {
        await Approve(AllSources);

        var ollama = (await Checks(_githubOnly, new FakeProbe(Answer: Unit.Value, Hang: true), TimeSpan.FromMilliseconds(50)).Run(Token)).Single(r => r.Name == "context:index-ollama");

        Assert.Equal((CheckState.Warn, "Ollama did not answer within 0 s"), (ollama.State, ollama.Detail));
    }

    [Fact, Trait("Lane", "unit")]
    public void Sync_with_no_feeds_is_ok()
    {
        var row = IndexSyncRules.Judge([]);

        Assert.Equal((CheckState.Ok, "no record source feeds a repo yet"), (row.State, row.Detail));
    }

    [Fact, Trait("Lane", "unit")]
    public void Sync_names_each_source_s_last_sync_and_is_ok_when_all_ended_clean()
    {
        var at = new DateTimeOffset(2026, 10, 28, 9, 5, 0, TimeSpan.Zero);

        var row = IndexSyncRules.Judge([SourceProgress.From(SourceId.From("github"), SyncPhase.Incremental, at, lastCode: null)]);

        Assert.Equal(new DoctorCheck("context:index-sync", CheckState.Ok, "record index: github synced 2026-10-28 09:05Z", Fix: null), row);
    }

    [Fact, Trait("Lane", "unit")]
    public void Sync_warns_for_a_source_not_synced_yet_or_whose_last_sync_failed()
    {
        var at = new DateTimeOffset(2026, 10, 28, 9, 5, 0, TimeSpan.Zero);

        var row = IndexSyncRules.Judge([SourceProgress.From(SourceId.From("github"), SyncPhase.Incremental, at, "SND-CONTEXT-010"), SourceProgress.From(SourceId.From("slack"), phase: null, lastSync: null, lastCode: null)]);

        Assert.Equal((CheckState.Warn, "record index: github last sync failed: SND-CONTEXT-010; slack not synced yet"), (row.State, row.Detail));
        Assert.Equal("sindri index sync, then sindri index status", row.Fix);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_time_capped_sync_is_ok_not_a_failure()
    {
        var at = new DateTimeOffset(2026, 10, 28, 9, 5, 0, TimeSpan.Zero);

        var row = IndexSyncRules.Judge([SourceProgress.From(SourceId.From("github"), SyncPhase.Incremental, at, "SND-CONTEXT-059")]);

        Assert.Equal((CheckState.Ok, "record index: github synced 2026-10-28 09:05Z"), (row.State, row.Detail));
    }

    [Fact, Trait("Lane", "unit")]
    public void Sync_names_the_projects_feed_as_its_own_source_and_says_when_the_issue_feed_is_re_reading()
    {
        var at = new DateTimeOffset(2026, 10, 28, 9, 5, 0, TimeSpan.Zero);

        var row = IndexSyncRules.Judge(
        [
            SourceProgress.From(SourceId.From("linear"), SyncPhase.Recent, lastSync: null, lastCode: null, stored: 1200),
            SourceProgress.From(SourceId.From("linear-projects"), SyncPhase.Incremental, at, lastCode: null),
        ]);

        Assert.Equal((CheckState.Warn, "record index: linear re-reading for project links, 1,200 issues stored; linear-projects synced 2026-10-28 09:05Z"), (row.State, row.Detail));
    }

    private IndexDoctorChecks Checks(PointerSecrets secrets, FakeProbe probe, TimeSpan? limit = null) =>
        new(_h.Profiles, new ContextScopes(_h.Profiles, _h.Callers), FakeContextEngine.Instance, secrets, probe, limit ?? TimeSpan.FromSeconds(3));

    private async Task Approve(string context)
    {
        var profile = new SindriProfile(1, "thor", Repos: _repos, Context: JsonDocument.Parse(context).RootElement.Clone());
        _ = await _h.Profiles.Save(new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml"), Token);
    }

    private sealed class FakeProbe(Result<Unit> Answer, bool Hang = false) : IRecordEmbedderProbe
    {
        public int Calls { get; private set; }

        public string Model => "nomic-embed-text:latest";

        public async ValueTask<Result<Unit>> Probe(CancellationToken cancellationToken)
        {
            Calls++;
            if (Hang)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            return Answer;
        }
    }
}
