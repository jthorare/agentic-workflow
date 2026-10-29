using System.Globalization;
using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>Health's Context index card (#1597): one line per source, so a source that is idle with nothing in it can't hide behind another that is pulling.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextIndexCardTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Linear_at_zero_while_github_pulls_reads_nothing_committed_and_needs_a_look()
    {
        var commit = _clock.GetUtcNow().AddMinutes(-3);
        _server.IndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK,
            $$"""{"sources":[{"source":"github","phase":"live","usable":true,"complete":false,"lastSync":null,"lastCode":null,"pull":"running","records":200,"lastCommit":"{{commit:O}}"},{"source":"linear","phase":"live","usable":false,"complete":false,"lastSync":null,"lastCode":null,"pull":"idle","records":0,"lastCommit":null}]}""");
        var card = new ContextIndexCardViewModel(_server, _clock);

        await card.Refresh(_api, Token);

        Assert.Equal(["github: pulling · 200 records · last commit 3 min ago", "linear: idle · 0 records · nothing committed yet"], card.Lines.Select(l => l.Text), StringComparer.Ordinal);
        Assert.Equal("needs a look", card.Word, StringComparer.Ordinal);
    }

    private static string Source(string name, string pull, int? records = null, string? lastCommit = null, string? lastCode = null, string? lastError = null, string? lastErrorText = null, string? nextTry = null) =>
        $$"""{"source":"{{name}}","phase":"live","usable":true,"complete":false,"lastSync":null,"lastCode":{{Json(lastCode)}},"pull":"{{pull}}","records":{{(records is { } r ? r.ToString(CultureInfo.InvariantCulture) : "null")}},"lastCommit":{{Json(lastCommit)}},"lastError":{{Json(lastError)}},"lastErrorText":{{Json(lastErrorText)}},"nextTry":{{Json(nextTry)}}}""";

    private static string Json(string? value) => value is null ? "null" : "\"" + value + "\"";

    private async Task<ContextIndexCardViewModel> Read(params string[] sources)
    {
        _server.IndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, $$"""{"sources":[{{string.Join(',', sources)}}]}""");
        var card = new ContextIndexCardViewModel(_server, _clock);
        await card.Refresh(_api, Token);
        return card;
    }

    [Fact]
    public async Task A_running_source_reads_pulling_with_its_records_and_the_age_of_its_last_commit()
    {
        var card = await Read(Source("github", "running", 200, _clock.GetUtcNow().AddHours(-2).ToString("O", CultureInfo.InvariantCulture)));

        var line = Assert.Single(card.Lines);
        Assert.Equal("github: pulling · 200 records · last commit 2 h ago", line.Text);
        Assert.Equal("Context index: github: pulling · 200 records · last commit 2 h ago", line.Name);
        Assert.Equal((Labels.CodeIndexOk, string.Empty, false), (card.Word, card.Message, card.HasMessage));
    }

    [Fact]
    public async Task A_running_source_with_nothing_yet_is_fine_because_it_just_started()
    {
        var card = await Read(Source("linear", "running", 0));

        Assert.Equal("linear: pulling · 0 records · nothing committed yet", Assert.Single(card.Lines).Text);
        Assert.Equal(Labels.CodeIndexOk, card.Word);
    }

    [Fact]
    public async Task An_idle_source_that_holds_records_is_fine()
    {
        var card = await Read(Source("slack", "idle", 1, _clock.GetUtcNow().AddSeconds(-5).ToString("O", CultureInfo.InvariantCulture)));

        Assert.Equal("slack: idle · 1 record · last commit under a minute ago", Assert.Single(card.Lines).Text);
        Assert.Equal(Labels.CodeIndexOk, card.Word);
    }

    [Fact]
    public async Task An_idle_source_capped_this_round_says_so()
    {
        var card = await Read(Source("github", "idle", 50, _clock.GetUtcNow().AddDays(-4).ToString("O", CultureInfo.InvariantCulture), lastCode: "SND-CONTEXT-059"));

        Assert.Equal("github: idle, at its cap this round · 50 records · last commit 4 d ago", Assert.Single(card.Lines).Text);
    }

    [Theory]
    [InlineData(4, "retrying in 4 min (SND-CONTEXT-041)")]
    [InlineData(0, "retrying in 1 min (SND-CONTEXT-041)")]
    public async Task A_source_in_backoff_says_the_minutes_to_its_retry_and_the_card_needs_a_look(int minutes, string expected)
    {
        var next = _clock.GetUtcNow().AddMinutes(minutes).AddSeconds(30).ToString("O", CultureInfo.InvariantCulture);

        var card = await Read(Source("slack", "backoff", lastError: "SND-CONTEXT-041", nextTry: next));

        Assert.Equal("slack: " + expected, Assert.Single(card.Lines).Text);
        Assert.Equal(Labels.CodeIndexNeedsLook, card.Word);
    }

    [Fact]
    public async Task A_failed_source_names_its_code_and_what_failed()
    {
        var card = await Read(Source("slack", "failed", 0, lastError: "SND-CONTEXT-041", lastErrorText: "the Slack token was refused"));

        Assert.Equal("slack: failed (SND-CONTEXT-041): the Slack token was refused · 0 records · nothing committed yet", Assert.Single(card.Lines).Text);
        Assert.Equal(Labels.CodeIndexNeedsLook, card.Word);
    }

    [Fact]
    public async Task A_pull_word_this_app_does_not_know_is_shown_as_served()
    {
        var card = await Read(Source("slack", "paused-by-owner"));

        Assert.Equal("slack: paused-by-owner", Assert.Single(card.Lines).Text);
    }

    [Fact]
    public async Task Todays_server_without_the_new_fields_shows_the_phase_and_is_fine()
    {
        _server.IndexStatusAnswer = RouteAnswer.SpecShaped("index-status", "today");
        var card = new ContextIndexCardViewModel(_server, _clock);

        await card.Refresh(_api, Token);

        Assert.Equal(["github: live", "linear: live", "slack: live"], card.Lines.Select(l => l.Text), StringComparer.Ordinal);
        Assert.Equal(Labels.CodeIndexOk, card.Word);
    }

    [Fact]
    public async Task Todays_server_with_no_phase_says_not_started_and_a_code_on_an_unusable_source_needs_a_look()
    {
        var card = await Read(
            """{"source":"slack","phase":null,"usable":false,"complete":false,"lastSync":null,"lastCode":null}""",
            """{"source":"linear","phase":"live","usable":false,"complete":false,"lastSync":null,"lastCode":"SND-CONTEXT-041"}""");

        Assert.Equal(["slack: not started", "linear: live"], card.Lines.Select(l => l.Text), StringComparer.Ordinal);
        Assert.Equal(Labels.CodeIndexNeedsLook, card.Word);
    }

    [Fact]
    public async Task No_sources_says_none_are_set_up_and_has_no_word()
    {
        var card = await Read();

        Assert.Empty(card.Lines);
        Assert.Equal((Labels.ContextIndexNone, true, string.Empty), (card.Message, card.HasMessage, card.Word));
    }

    [Fact]
    public async Task A_read_error_says_it_cant_be_read_and_clears_the_lines()
    {
        _ = await Read(Source("github", "running", 1));
        var card = new ContextIndexCardViewModel(_server, _clock);
        Assert.Equal((Labels.ReadingContextIndex, true), (card.Message, card.HasMessage));
        _server.IndexStatusAnswer = RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-API-500", "broken");

        await card.Refresh(_api, Token);

        Assert.Empty(card.Lines);
        Assert.Equal((Labels.ContextIndexUnreadable, string.Empty), (card.Message, card.Word));
    }

    [Fact]
    public void The_heading_and_name_prefix_are_the_cards_own_words()
    {
        var card = new ContextIndexCardViewModel(_server, _clock);

        Assert.Equal(("Context index", "Context index: "), (card.Heading, Labels.ContextIndexNamePrefix));
    }

    [Fact]
    public async Task Health_adds_the_context_index_to_its_headline_when_the_card_needs_a_look()
    {
        var harness = new SystemTestHarness();
        await harness.Show(A.Status() with { Health = new HealthView("healthy", []) });
        harness.Server.IndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, $$"""{"sources":[{{Source("linear", "idle", 0)}}]}""");

        await harness.Vm.ContextIndex.Refresh(A.Record().Url, Token);

        Assert.Equal(Labels.CodeIndexNeedsLook, harness.Vm.ContextIndex.Word);
        Assert.Equal("Something needs a look: context index.", harness.Vm.Sentence);
        Assert.True(harness.Vm.ToneWarn);
    }
}
