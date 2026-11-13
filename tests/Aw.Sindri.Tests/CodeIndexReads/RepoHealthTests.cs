using System.Text.Json;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.CodeIndexReads;
using IndexRepoHead = Aw.CodeIndex.Core.Model.RepoHead;
using RepoName = Aw.CodeIndex.Core.Values.RepoName;

namespace Aw.Sindri.Tests.CodeIndexReads;

/// <summary>The overlay on a repo's index status (code index spec §7.3): stale, building and waiting from the heads and the jobs, and the embeddings percent from the layer's own count.</summary>
public sealed class RepoHealthTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly RepoName _repo = RepoName.From("demo");
    private static readonly CommitSha _old = CommitSha.From(new string('1', 40));
    private static readonly CommitSha _new = CommitSha.From(new string('2', 40));
    private static long _next;

    private static RepoStatus Built(string embeddings = "ok", string detail = "100 of 100 embedded") => new(_repo, IndexState.Ok, _old, _now.AddMinutes(-3),
        [new LayerStatus(LayerName.Structure, LayerState.Ok, Code: null, "ok"), new LayerStatus(LayerName.Embeddings, Enum.Parse<LayerState>(embeddings, ignoreCase: true), Code: null, detail)],
        new IndexCounts(Files: 1, Symbols: 2, Edges: 3));

    private static JobRow Job(JobState state, TimeSpan endedAgo, string? outcome = null) => new(JobId.From("JOB-" + (++_next).ToString("x32", System.Globalization.CultureInfo.InvariantCulture)), JobKind.From("BuildCodeIndex"), JobKey.From("index:demo"), Item: null, Repo: null, Service: null, state,
        "service:code-index-main-move", _now - endedAgo - TimeSpan.FromSeconds(5), state == JobState.Running ? null : _now - endedAgo, [], Rounds: 0, Summary: null, Code: null,
        outcome is null ? null : JsonSerializer.SerializeToElement(new { outcome }), CancelledBy: null, _next);

    private static RepoHealth Of(RepoStatus status, IndexRepoHead? head, params JobRow[] jobs) => RepoHealth.Of(status, head, jobs, _now);

    [Fact, Trait("Lane", "unit")]
    public void A_current_index_is_ok_and_pending_embeddings_read_as_a_percent()
    {
        var health = Of(Built("pending", "31 of 100 embedded"), new IndexRepoHead(_repo, _old, OnDefaultBranch: true));

        Assert.Equal(("ok", 31, null), (health.State, health.EmbeddingsPercent, health.Waiting));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_head_past_the_index_is_stale()
    {
        var health = Of(Built(), new IndexRepoHead(_repo, _new, OnDefaultBranch: true));

        Assert.Equal(("stale", 100), (health.State, health.EmbeddingsPercent));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_running_build_is_building_even_when_the_index_is_current()
    {
        var health = Of(Built(), new IndexRepoHead(_repo, _old, OnDefaultBranch: true), Job(JobState.Running, TimeSpan.Zero));

        Assert.Equal("building", health.State);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_failed_build_in_backoff_is_waiting_with_the_retry_words()
    {
        var health = Of(Built(), new IndexRepoHead(_repo, _new, OnDefaultBranch: true), Job(JobState.Failed, TimeSpan.FromSeconds(10)));

        Assert.Equal("waiting", health.State);
        Assert.StartsWith("retrying in ", health.Waiting, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_repo_never_built_is_missing_and_has_no_percent()
    {
        var health = Of(new RepoStatus(_repo, IndexState.Missing, Commit: null, BuiltAt: null, [], Counts: null), head: null);

        Assert.Equal(("missing", null), (health.State, health.EmbeddingsPercent));
    }

    // #3342: the first build of a repo on its default branch starts on code-index-main-move's next tick, so the card can say "first build queued" without guessing.
    [Fact, Trait("Lane", "unit")]
    public void A_repo_with_no_index_and_its_head_on_the_default_branch_is_queued()
    {
        var health = Of(Missing(), new IndexRepoHead(_repo, _new, OnDefaultBranch: true));

        Assert.Equal(("queued", null), (health.State, health.Waiting));
    }

    // #3181: off its default branch no build ever starts, so the repo must stay missing ("needs a look"), never queued.
    [Fact, Trait("Lane", "unit")]
    public void A_repo_with_no_index_off_its_default_branch_stays_missing()
        => Assert.Equal("missing", Of(Missing(), new IndexRepoHead(_repo, _new, OnDefaultBranch: false)).State);

    [Fact, Trait("Lane", "unit")]
    public void A_first_build_that_failed_inside_its_backoff_is_waiting_not_queued()
        => Assert.Equal("waiting", Of(Missing(), new IndexRepoHead(_repo, _new, OnDefaultBranch: true), Job(JobState.Failed, TimeSpan.FromSeconds(10))).State);

    [Fact, Trait("Lane", "unit")]
    public void A_first_build_that_runs_is_building_not_queued()
        => Assert.Equal("building", Of(Missing(), new IndexRepoHead(_repo, _new, OnDefaultBranch: true), Job(JobState.Running, TimeSpan.Zero)).State);

    private static RepoStatus Missing() => new(_repo, IndexState.Missing, Commit: null, BuiltAt: null, [], Counts: null);

    [Fact, Trait("Lane", "unit")]
    public void A_detail_that_is_not_a_count_gives_no_percent() =>
        Assert.Null(Of(Built("unavailable", "SND-CODEIDX-006"), new IndexRepoHead(_repo, _old, OnDefaultBranch: true)).EmbeddingsPercent);
}
