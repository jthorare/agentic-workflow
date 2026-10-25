using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Testing;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.CodeIndexJobs;
using IndexRepoHead = Aw.CodeIndex.Core.Model.RepoHead;
using RepoStatus = Aw.CodeIndex.Core.Facade.RepoStatus;

namespace Aw.Sindri.Tests.CodeIndexJobs;

/// <summary>The code index's doctor rows (code index spec §8): the words keyed by the §7.3 state.</summary>
public sealed class CodeIndexDoctorTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly RepoName _repo = RepoName.From("demo");
    private static readonly CommitSha _old = CommitSha.From(new string('1', 40));
    private static readonly CommitSha _new = CommitSha.From(new string('2', 40));

    private static LayerStatus Ok(LayerName layer) => new(layer, LayerState.Ok, Code: null, "ok");

    private static RepoStatus Built(params LayerStatus[] layers) => new(_repo, IndexState.Ok, _old, _now.AddMinutes(-3), layers, new IndexCounts(Files: 1, Symbols: 2, Edges: 3));

    private static DoctorCheck RepoRow(RepoStatus status, IndexRepoHead? head = null) =>
        CodeIndexDoctor.Rows([status], head is null ? [] : [head], [], _now).Single(static c => c.Name == "code-index:demo");

    [Fact, Trait("Lane", "unit")]
    public void A_current_index_is_ok_with_its_commit_age_and_layer_words()
    {
        var row = RepoRow(Built(Ok(LayerName.Structure), Ok(LayerName.Graph), new LayerStatus(LayerName.Embeddings, LayerState.Pending, Code: null, "pending")), new IndexRepoHead(_repo, _old, OnDefaultBranch: true));

        Assert.Equal(CheckState.Ok, row.State);
        Assert.Equal("1111111, 3 min old, structure ok · graph ok · embeddings pending", row.Detail);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_head_past_the_index_is_a_warning_that_names_both_commits()
    {
        var row = RepoRow(Built(Ok(LayerName.Structure)), new IndexRepoHead(_repo, _new, OnDefaultBranch: true));

        Assert.Equal(CheckState.Warn, row.State);
        Assert.Contains("stale: index at 1111111, head at 2222222", row.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_checkout_off_main_says_so()
    {
        var row = RepoRow(Built(Ok(LayerName.Structure)), new IndexRepoHead(_repo, _new, OnDefaultBranch: false));

        Assert.Equal((CheckState.Warn, "checkout is not on main; the index at 1111111 is served"), (row.State, row.Detail));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_damaged_index_fails_and_says_it_rebuilds()
    {
        var row = RepoRow(new RepoStatus(_repo, IndexState.Damaged, Commit: null, BuiltAt: null, [], Counts: null));

        Assert.Equal((CheckState.Fail, "index damaged, rebuilding (SND-CODEIDX-010)"), (row.State, row.Detail));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_repo_with_no_index_is_a_queued_first_build()
    {
        var row = RepoRow(new RepoStatus(_repo, IndexState.Missing, Commit: null, BuiltAt: null, [], Counts: null));

        Assert.Equal((CheckState.Warn, "first build queued"), (row.State, row.Detail));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unavailable_graph_is_a_warning_with_the_codes_fix_and_never_a_failure()
    {
        var rows = CodeIndexDoctor.Rows([Built(Ok(LayerName.Structure), new LayerStatus(LayerName.Graph, LayerState.Unavailable, "SND-CODEIDX-007", "no sandbox"))], [], [], _now);

        var graph = rows.Single(static c => c.Name == "code-index:graphify");
        Assert.Equal(CheckState.Warn, graph.State);
        Assert.Contains("bubblewrap", graph.Fix, StringComparison.Ordinal);
        Assert.DoesNotContain(rows, static c => c.State == CheckState.Fail);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_stored_pin_mismatch_on_the_embeddings_layer_warns_on_the_ollama_row()
    {
        var rows = CodeIndexDoctor.Rows([Built(Ok(LayerName.Structure), new LayerStatus(LayerName.Embeddings, LayerState.Unavailable, "SND-CODEIDX-009", "model digest"))], [], [], _now);

        Assert.Equal(CheckState.Warn, rows.Single(static c => c.Name == "code-index:ollama").State);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_repos_is_one_ok_row() =>
        Assert.Equal([("code-index", CheckState.Ok)], CodeIndexDoctor.Rows([], [], [], _now).Select(static c => (c.Name, c.State)));

    private static DoctorCheck Row(string name, CodeIndexDoctor.Probes probes) =>
        CodeIndexDoctor.Rows([Built(Ok(LayerName.Structure))], [], [], _now, probes).Single(c => c.Name == name);

    private static CodeIndexDoctor.Probes Probes(LockState state, Kernel.Result<Kernel.Unit>? model = null, bool enabled = true) =>
        new(new CodeIndexDoctor.ModelProbe(enabled, model ?? Kernel.Unit.Value), state);

    [Theory, Trait("Lane", "unit")]
    [InlineData(LockState.Matches, CheckState.Ok, "parses and matches")]
    [InlineData(LockState.Mismatch, CheckState.Warn, "does not match")]
    [InlineData(LockState.Unparsable, CheckState.Warn, "does not parse")]
    [InlineData(LockState.Missing, CheckState.Warn, "was not found")]
    public void The_lock_row_reports_what_the_lock_file_check_found(LockState state, CheckState expected, string words)
    {
        var row = Row("code-index:lock", Probes(state));

        Assert.Equal(expected, row.State);
        Assert.Contains(words, row.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unreachable_embedding_server_is_a_warning_from_the_live_probe()
    {
        var row = Row("code-index:ollama", Probes(LockState.Matches, CodeIndex.Core.CodeIndexErrors.EmbeddingServerFailed.With("no answer")));

        Assert.Equal(CheckState.Warn, row.State);
        Assert.Contains("SND-CODEIDX-006", row.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_reachable_server_with_the_pinned_model_is_ok_and_embeddings_off_is_a_warning()
    {
        Assert.Equal(CheckState.Ok, Row("code-index:ollama", Probes(LockState.Matches)).State);
        Assert.Equal(CheckState.Warn, Row("code-index:ollama", Probes(LockState.Matches, enabled: false)).State);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_live_only_repo_is_checked_too_owner_q102_CIX_L59()
    {
        var rig = new CodeIndexJobsRig();
        await rig.Approve(TestContext.Current.CancellationToken);
        var fresh = new SindriProfile(1, "thor", Repos: [new RepoEntry("demo", "/work/demo"), new RepoEntry("other", "/work/other"), new RepoEntry("fresh", "/work/fresh")]);
        rig.Harness.Profiles.Live = new LoadedProfile(fresh, Core.Ids.ProfileHash.From(new string('a', 64)), "/home/profile.yaml");
        _ = rig.Index.WithIndex(CodeIndexJobsRig.Fresh, CodeIndexJobsRig.Old);
        var doctor = new CodeIndexDoctor(rig.Index, rig.Heads, rig.Jobs, new FakeEmbedder(), new FakePins(), rig.Harness.Clock, rig.Harness.Profiles);
        var approved = (await rig.Harness.Profiles.Latest(TestContext.Current.CancellationToken)).Value;

        var rows = await doctor.Check(approved, TestContext.Current.CancellationToken);

        Assert.Contains(rows, static r => r.Name == "code-index:fresh");
    }
}
