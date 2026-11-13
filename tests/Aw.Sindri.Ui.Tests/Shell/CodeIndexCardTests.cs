using System.Globalization;
using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>Health's Code index card (code-index spec §7.3, CI-10b): the words keyed by the index's state, from the status read.</summary>
[Lane(TestLane.Unit)]
public sealed class CodeIndexCardTests
{
    private const string Sha = "abcdef0123456789abcdef0123456789abcdef01";
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Layer(string layer, string state, string? code = null, string detail = "ok") =>
        $$"""{"layer":"{{layer}}","state":"{{state}}","code":{{(code is null ? "null" : "\"" + code + "\"")}},"detail":"{{detail}}"}""";

    private string Repo(string name, string state, string? commit, int builtMinutesAgo, params string[] layers) =>
        $$"""{"repo":"{{name}}","state":"{{state}}","commit":{{(commit is null ? "null" : "\"" + commit + "\"")}},"builtAt":"{{_clock.GetUtcNow().AddMinutes(-builtMinutesAgo):O}}","layers":[{{string.Join(',', layers)}}],"counts":null}""";

    private static string Health(string name, string state, int? embeddingsPercent = null, string? waiting = null, bool paused = false, bool needsLook = false, string? lookWords = null) =>
        $$"""{"repo":"{{name}}","state":"{{state}}","embeddingsPercent":{{(embeddingsPercent is { } p ? p.ToString(CultureInfo.InvariantCulture) : "null")}},"waiting":{{(waiting is null ? "null" : "\"" + waiting + "\"")}},"paused":{{(paused ? "true" : "false")}},"needsLook":{{(needsLook ? "true" : "false")}},"lookWords":{{(lookWords is null ? "null" : "\"" + lookWords + "\"")}}}""";

    private async Task<CodeIndexCardViewModel> Read(params string[] repos) => await ReadWithHealth(repos, []);

    private async Task<CodeIndexCardViewModel> ReadWithHealth(string[] repos, string[] health)
    {
        _server.CodeIndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, $$"""{"repos":[{{string.Join(',', repos)}}],"health":[{{string.Join(',', health)}}]}""");
        var card = new CodeIndexCardViewModel(_server, _clock);
        await card.Refresh(_api, Token);
        return card;
    }

    [Fact]
    public async Task A_healthy_index_reads_repo_commit_age_and_each_layer()
    {
        var card = await Read(Repo("repo", "ok", Sha, 3, Layer("structure", "ok"), Layer("graph", "ok"), Layer("embeddings", "pending", detail: "31 of 100 embedded")));

        var line = Assert.Single(card.Lines);
        Assert.Equal("repo @ abcdef0, 3 min old, structure ok · graph ok · embeddings pending 31%", line.Text);
        Assert.Equal("Code index: repo @ abcdef0, 3 min old, structure ok · graph ok · embeddings pending 31%", line.Name);
        Assert.Equal((Labels.CodeIndexOk, string.Empty, false), (card.Word, card.Message, card.HasMessage));
    }

    [Fact]
    public async Task A_layer_that_cannot_be_built_says_unavailable_with_its_code_and_the_card_needs_a_look()
    {
        var card = await Read(Repo("repo", "ok", Sha, 3, Layer("structure", "ok"), Layer("graph", "unavailable", "SND-CODEIDX-020"), Layer("embeddings", "disabled")));

        Assert.Equal("repo @ abcdef0, 3 min old, structure ok · graph unavailable (SND-CODEIDX-020) · embeddings off", Assert.Single(card.Lines).Text);
        Assert.Equal(Labels.CodeIndexNeedsLook, card.Word);
    }

    [Fact]
    public async Task Clones_and_deps_show_only_when_they_need_a_look()
    {
        var card = await Read(Repo("repo", "ok", Sha, 1, Layer("structure", "ok"), Layer("clones", "ok"), Layer("deps", "unavailable", "SND-CODEIDX-021")));

        Assert.Equal("repo @ abcdef0, 1 min old, structure ok · deps unavailable (SND-CODEIDX-021)", Assert.Single(card.Lines).Text);
    }

    [Fact]
    public async Task A_repo_flagged_needs_a_look_says_its_look_words_on_its_line()
    {
        const string Words = "demo: first build queued for 40 min; check that code-index-main-move is running";
        var card = await ReadWithHealth([Repo("demo", "missing", commit: null, 0)], [Health("demo", "queued", needsLook: true, lookWords: Words)]);

        Assert.Equal(Words, Assert.Single(card.Lines).Text);
        Assert.Equal(Labels.CodeIndexNeedsLook, card.Word);
    }

    private const string BehindWords = "demo: code index is behind main; rebuild it from the System page";

    [Fact]
    public async Task A_built_stale_repo_flagged_needs_a_look_keeps_its_commit_age_and_layers()
    {
        var built = new[] { Repo("demo", "ok", Sha, 3, Layer("structure", "ok"), Layer("graph", "ok")) };
        var plain = Assert.Single((await ReadWithHealth(built, [Health("demo", "stale")])).Lines);
        var flagged = Assert.Single((await ReadWithHealth(built, [Health("demo", "stale", needsLook: true, lookWords: BehindWords)])).Lines);

        Assert.Equal(plain.Text, flagged.Text);
        Assert.Contains("@ abcdef0", flagged.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("rebuild", flagged.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_paused_stale_repo_flagged_needs_a_look_still_says_it_is_paused()
    {
        var card = await ReadWithHealth([Repo("demo", "ok", Sha, 3, Layer("structure", "ok"))], [Health("demo", "stale", paused: true, needsLook: true, lookWords: BehindWords)]);

        var line = Assert.Single(card.Lines);
        Assert.Contains(Labels.CodeIndexPaused, line.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("rebuild", line.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_built_repo_with_an_unavailable_graph_flagged_needs_a_look_keeps_its_normal_line()
    {
        var built = new[] { Repo("demo", "ok", Sha, 3, Layer("structure", "ok"), Layer("graph", "unavailable", "SND-CODEIDX-020")) };
        var plain = Assert.Single((await ReadWithHealth(built, [Health("demo", "ok")])).Lines);
        var flagged = Assert.Single((await ReadWithHealth(built, [Health("demo", "ok", needsLook: true, lookWords: "demo: code graph is unavailable")])).Lines);

        Assert.Equal(plain.Text, flagged.Text);
        Assert.Contains("@ abcdef0", flagged.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_queued_repo_the_server_does_not_flag_still_says_build_queued()
    {
        var card = await ReadWithHealth([Repo("demo", "missing", commit: null, 0)], [Health("demo", "queued")]);

        Assert.Equal("demo: " + Labels.CodeIndexState("queued"), Assert.Single(card.Lines).Text);
        Assert.Equal(Labels.CodeIndexQueued, card.Word);
    }

    [Theory]
    [InlineData("missing", "repo: no index yet")]
    [InlineData("damaged", "repo: damaged, rebuilding on the next check")]
    [InlineData("building", "repo: building")]
    [InlineData("waiting", "repo: waiting to retry")]
    [InlineData("something-new", "repo: something-new")]
    public async Task An_index_with_no_commit_says_its_state_in_words(string state, string expected)
    {
        var card = await Read(Repo("repo", state, commit: null, 0));

        Assert.Equal(expected, Assert.Single(card.Lines).Text);
        Assert.Equal(Labels.CodeIndexNeedsLook, card.Word);
    }

    // #3399: missing comes paused or off the default branch too, where no build is coming; only queued may promise one.
    [Fact]
    public void Missing_never_promises_a_build()
        => Assert.DoesNotContain("queued", Labels.CodeIndexState("missing"), StringComparison.Ordinal);

    [Fact]
    public async Task A_stale_index_names_the_state_beside_its_age()
    {
        var card = await Read(Repo("repo", "stale", Sha, 120, Layer("structure", "ok")));

        Assert.Equal("repo @ abcdef0, 2 h old (behind main), structure ok", Assert.Single(card.Lines).Text);
        Assert.Equal(Labels.CodeIndexNeedsLook, card.Word);
    }

    [Fact]
    public async Task Each_repo_has_its_own_line()
    {
        var card = await Read(Repo("a", "ok", Sha, 5, Layer("structure", "ok")), Repo("b", "missing", commit: null, 0));

        Assert.Equal(["a @ abcdef0, 5 min old, structure ok", "b: no index yet"], card.Lines.Select(l => l.Text), StringComparer.Ordinal);
    }

    [Fact]
    public async Task No_repos_says_so()
    {
        var card = await Read();

        Assert.Empty(card.Lines);
        Assert.Equal((Labels.CodeIndexNone, string.Empty), (card.Message, card.Word));
    }

    [Fact]
    public async Task A_failed_read_says_it_cannot_be_read_and_drops_the_old_lines()
    {
        var card = await Read(Repo("repo", "ok", Sha, 3, Layer("structure", "ok")));
        _server.CodeIndexStatusAnswer = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-001", "not running");

        await card.Refresh(_api, Token);

        Assert.Empty(card.Lines);
        Assert.Equal((Labels.CodeIndexUnreadable, true), (card.Message, card.HasMessage));
    }

    [Fact]
    public void Before_the_first_read_it_says_it_is_reading()
    {
        var card = new CodeIndexCardViewModel(_server, _clock);

        Assert.Equal((Labels.ReadingCodeIndex, 0), (card.Message, card.Lines.Count));
    }

    [Theory]
    [InlineData("stale", "repo @ abcdef0, 3 min old (behind main), structure ok")]
    [InlineData("building", "repo @ abcdef0, 3 min old (building), structure ok")]
    public async Task A_health_overlay_overrides_the_file_state(string state, string expected)
    {
        var card = await ReadWithHealth([Repo("repo", "ok", Sha, 3, Layer("structure", "ok"))], [Health("repo", state)]);

        Assert.Equal(expected, Assert.Single(card.Lines).Text);
        Assert.Equal(Labels.CodeIndexNeedsLook, card.Word);
    }

    [Theory]
    [InlineData("retrying in 4 min")]
    [InlineData("retrying in 1 min")]
    public async Task A_waiting_repo_shows_the_servers_waiting_words(string waiting)
    {
        var card = await ReadWithHealth([Repo("repo", "ok", Sha, 3, Layer("structure", "ok"))], [Health("repo", "waiting", waiting: waiting)]);

        Assert.Equal($"repo @ abcdef0, 3 min old ({waiting}), structure ok", Assert.Single(card.Lines).Text);
    }

    // #3561: the doctor names the pause for the same repo; the card must not promise a retry or a build.
    [Fact]
    public async Task A_paused_repo_with_no_index_says_paused()
    {
        var card = await ReadWithHealth([Repo("repo", "missing", commit: null, 0)], [Health("repo", "missing", paused: true)]);

        Assert.Equal("repo: no index yet · paused", Assert.Single(card.Lines).Text);
    }

    // #3576: the pause is one service, so the card shows its resume command once, not on every repo line.
    // why: Aw.Sindri.Ui references no Sindri project, so the service name is spelled out here once, as the doctor row spells it.
    [Fact]
    public async Task Three_paused_repos_show_one_command_on_the_card_and_none_on_the_lines()
    {
        var card = await ReadWithHealth([Repo("a", "missing", commit: null, 0), Repo("b", "missing", commit: null, 0), Repo("c", "missing", commit: null, 0)],
            [Health("a", "missing", paused: true), Health("b", "missing", paused: true), Health("c", "missing", paused: true)]);

        Assert.Equal(("sindri services resume code-index-main-move", true), (card.PausedCommand, card.HasPausedCommand));
        Assert.Equal(3, card.Lines.Count);
        Assert.All(card.Lines, l => Assert.DoesNotContain("resume", l.Text, StringComparison.OrdinalIgnoreCase));
        Assert.All(card.Lines, l => Assert.EndsWith("· paused", l.Text, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_card_with_no_paused_repo_has_no_command()
    {
        var card = await ReadWithHealth([Repo("repo", "ok", Sha, 3, Layer("structure", "ok"))], [Health("repo", "ok")]);

        Assert.Equal((string.Empty, false), (card.PausedCommand, card.HasPausedCommand));
    }

    [Fact]
    public async Task A_paused_repo_in_backoff_never_reads_waiting_to_retry()
    {
        var card = await ReadWithHealth([Repo("repo", "ok", Sha, 3, Layer("structure", "ok"))], [Health("repo", "stale", paused: true)]);

        Assert.Equal("repo @ abcdef0, 3 min old (behind main · paused), structure ok", Assert.Single(card.Lines).Text);
        Assert.Equal(Labels.CodeIndexNeedsLook, card.Word);
    }

    [Fact]
    public async Task A_waiting_repo_with_no_commit_shows_the_servers_waiting_words()
    {
        const string waiting = "retrying in 2 min";

        var card = await ReadWithHealth([Repo("repo", "missing", commit: null, 0)], [Health("repo", "waiting", waiting: waiting)]);

        Assert.Equal("repo: " + waiting, Assert.Single(card.Lines).Text);
    }

    [Fact]
    public async Task The_embeddings_percent_shows_when_the_health_carries_one()
    {
        var card = await ReadWithHealth([Repo("repo", "ok", Sha, 3, Layer("structure", "ok"))], [Health("repo", "ok", embeddingsPercent: 42)]);

        Assert.Equal("repo @ abcdef0, 3 min old, structure ok · embeddings 42%", Assert.Single(card.Lines).Text);
    }

    [Fact]
    public async Task A_healthy_overlay_changes_nothing()
    {
        var card = await ReadWithHealth([Repo("repo", "ok", Sha, 3, Layer("structure", "ok"))], [Health("repo", "ok")]);

        Assert.Equal(("repo @ abcdef0, 3 min old, structure ok", Labels.CodeIndexOk), (Assert.Single(card.Lines).Text, card.Word));
    }
}
