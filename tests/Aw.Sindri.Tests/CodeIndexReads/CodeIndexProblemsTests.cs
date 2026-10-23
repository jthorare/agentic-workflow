using System.Text.Json;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.Sindri.Core.Health;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.CodeIndexReads;
using IndexRepoHead = Aw.CodeIndex.Core.Model.RepoHead;
using RepoName = Aw.CodeIndex.Core.Values.RepoName;

namespace Aw.Sindri.Tests.CodeIndexReads;

/// <summary>The health row a repo's code index makes: stale, damaged, retrying a build and a layer unavailable are problems; missing, building, waiting for a slot and pending embeddings are not.</summary>
public sealed class CodeIndexProblemsTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly RepoName _repo = RepoName.From("demo");
    private static readonly CommitSha _old = CommitSha.From(new string('1', 40));
    private static readonly CommitSha _new = CommitSha.From(new string('2', 40));
    private static long _next;

    private static LayerStatus Structure() => new(LayerName.Structure, LayerState.Ok, Code: null, "ok");

    private static RepoStatus Built(params LayerStatus[] extra) => new(_repo, IndexState.Ok, _old, _now.AddMinutes(-3), [Structure(), .. extra], new IndexCounts(Files: 1, Symbols: 2, Edges: 3));

    private static RepoStatus Damaged() => new(_repo, IndexState.Damaged, Commit: null, BuiltAt: null, [], Counts: null);

    private static RepoStatus Missing() => new(_repo, IndexState.Missing, Commit: null, BuiltAt: null, [], Counts: null);

    private static JobRow Job(JobState state, TimeSpan endedAgo, string? outcome = null) => new(JobId.From("JOB-" + (++_next).ToString("x32", System.Globalization.CultureInfo.InvariantCulture)), JobKind.From("BuildCodeIndex"), JobKey.From("index:demo"), Item: null, Repo: null, Service: null, state,
        "service:code-index-main-move", _now - endedAgo - TimeSpan.FromSeconds(5), state == JobState.Running ? null : _now - endedAgo, [], Rounds: 0, Summary: null, Code: null,
        outcome is null ? null : JsonSerializer.SerializeToElement(new { outcome }), CancelledBy: null, _next);

    private static IndexRepoHead Moved() => new(_repo, _new, OnDefaultBranch: true);

    private static IndexRepoHead Current() => new(_repo, _old, OnDefaultBranch: true);

    private static HealthProblem? Problem(RepoStatus status, IndexRepoHead? head, params JobRow[] jobs) =>
        CodeIndexProblems.For(status, RepoHealth.Of(status, head, jobs, _now));

    [Fact, Trait("Lane", "unit")]
    public void A_stale_index_asks_for_a_rebuild_from_the_system_page()
    {
        var problem = Problem(Built(), Moved());

        Assert.Equal(("SND-CODEIDX-014", "demo: code index is behind main; rebuild it from the System page", "the code index for demo is behind main"), (problem?.Code, problem?.Text, problem?.Clause));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_damaged_index_rebuilds_on_the_next_check()
    {
        var problem = Problem(Damaged(), head: null);

        Assert.Equal(("SND-CODEIDX-010", "demo: code index is damaged; it rebuilds on the next check", "the code index for demo is damaged"), (problem?.Code, problem?.Text, problem?.Clause));
    }

    [Fact, Trait("Lane", "unit")]
    public void Failed_builds_inside_their_backoff_say_when_the_next_try_is()
    {
        var problem = Problem(Built(), Moved(), Job(JobState.Failed, TimeSpan.FromSeconds(30)));

        Assert.Equal("SND-CODEIDX-015", problem?.Code);
        Assert.Matches(@"^demo: code index build failed; retrying in \d+ min$", problem?.Text);
        Assert.Equal("the code index for demo is retrying a build", problem?.Clause);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unavailable_layer_is_a_problem_with_the_layers_own_code_kept_out_of_the_words()
    {
        var problem = Problem(Built(new LayerStatus(LayerName.Graph, LayerState.Unavailable, "SND-CODEIDX-008", "graphify is missing")), Current());

        Assert.Equal(("SND-CODEIDX-008", "demo: code graph is unavailable", "the code index for demo is unavailable"), (problem?.Code, problem?.Text, problem?.Clause));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_graph_not_built_by_this_version_is_no_problem()
        => Assert.Null(Problem(Built(new LayerStatus(LayerName.Graph, LayerState.Unavailable, Code: null, "not built by this version")), Current()));

    [Fact, Trait("Lane", "unit")]
    public void Unavailable_embeddings_are_no_problem_here_because_doctor_names_the_embedding_server()
        => Assert.Null(Problem(Built(new LayerStatus(LayerName.Embeddings, LayerState.Unavailable, "SND-CODEIDX-006", "the embedding server refused")), Current()));

    [Fact, Trait("Lane", "unit")]
    public void A_layer_unavailable_beats_a_stale_index()
    {
        var problem = Problem(Built(new LayerStatus(LayerName.Graph, LayerState.Unavailable, "SND-CODEIDX-008", "graphify is missing")), Moved());

        Assert.Equal("SND-CODEIDX-008", problem?.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_retry_beats_an_unavailable_layer()
    {
        var problem = Problem(Built(new LayerStatus(LayerName.Graph, LayerState.Unavailable, "SND-CODEIDX-008", "graphify is missing")), Moved(), Job(JobState.Failed, TimeSpan.FromSeconds(30)));

        Assert.Equal("SND-CODEIDX-015", problem?.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_current_index_with_pending_embeddings_is_no_problem()
        => Assert.Null(Problem(Built(new LayerStatus(LayerName.Embeddings, LayerState.Pending, Code: null, "31 of 100 embedded")), Current()));

    [Fact, Trait("Lane", "unit")]
    public void A_missing_index_is_no_problem()
        => Assert.Null(Problem(Missing(), Current()));

    [Fact, Trait("Lane", "unit")]
    public void A_running_build_is_no_problem_even_when_the_index_is_behind()
        => Assert.Null(Problem(Built(), Moved(), Job(JobState.Running, TimeSpan.Zero)));

    [Fact, Trait("Lane", "unit")]
    public void Waiting_for_a_free_build_slot_is_no_problem()
        => Assert.Null(Problem(Built(), Moved(), Job(JobState.Succeeded, TimeSpan.FromSeconds(30), outcome: "deferred")));

    [Fact, Trait("Lane", "unit")]
    public void A_current_healthy_index_is_no_problem()
        => Assert.Null(Problem(Built(), Current()));
}
