using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>Deciding from the row and the decision sheet (U3' T6): A and X send Decide once with the option, a refusal shows as served.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellDecideTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> WithDecision()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal, Token);

        shell.ShowNeedsYou(new NeedsYouFeed([NeedsYouRows.Decision()], ""));
        _ = shell.Press("J");
        return shell;
    }

    [Fact]
    public Task X_on_a_decision_row_sends_Decide_with_the_rejectLike_option_once() => UiThread.Run(async () =>
    {
        var shell = await WithDecision();

        Assert.True(shell.Press("X"));
        await shell.Decision.Settled;
        Assert.True(shell.Press("X"));
        await shell.Decision.Settled;

        Assert.Equal([("DEC-1", "decline")], [.. _server.DecideCalls.Select(c => (c.Decision, c.Request.Choice))]);
    }, Token);

    [Fact]
    public Task A_sends_nothing_until_the_decisions_sheet_is_open() => UiThread.Run(async () =>
    {
        var shell = await WithDecision();

        Assert.False(shell.Press("A"));
        await shell.Decision.Settled;

        Assert.Empty(_server.DecideCalls);
    }, Token);

    [Fact]
    public Task A_with_the_sheet_open_accepts_the_recommended_option() => UiThread.Run(async () =>
    {
        var shell = await WithDecision();
        shell.Navigate(new DeepLink(DeepLinkKind.Decision, "DEC-1"));

        Assert.True(shell.Press("A"));
        await shell.Decision.Settled;

        Assert.Equal([("DEC-1", "accept")], [.. _server.DecideCalls.Select(c => (c.Decision, c.Request.Choice))]);
        Assert.NotEmpty(shell.Decision.Outcome);
    }, Token);

    [Fact]
    public Task The_sheet_has_one_button_per_option_as_served_and_the_first_is_recommended() => UiThread.Run(async () =>
    {
        var shell = await WithDecision();

        shell.Navigate(new DeepLink(DeepLinkKind.Decision, "DEC-1"));

        Assert.Equal([("Accept", true), ("Decline", false)], [.. shell.Decision.Options.Select(o => (o.Label, o.IsRecommended))]);
        Assert.Equal("Re-scope: drop the iOS head from this item", shell.Decision.Question);
    }, Token);

    [Fact]
    public Task An_option_press_sends_the_option_and_the_note() => UiThread.Run(async () =>
    {
        var shell = await WithDecision();
        shell.Navigate(new DeepLink(DeepLinkKind.Decision, "DEC-1"));
        shell.Decision.Note = "agreed in standup";

        await shell.Decision.Choose("decline");

        var (_, request) = Assert.Single(_server.DecideCalls);
        Assert.Equal(("decline", "agreed in standup"), (request.Choice, request.Note));
    }, Token);

    [Fact]
    public Task A_conflict_shows_the_served_detail_inline_and_opens_the_sheet() => UiThread.Run(async () =>
    {
        var shell = await WithDecision();
        _server.DecideAnswers["DEC-1"] = RouteAnswer.Api("decisions-id", "decided-elsewhere");

        Assert.True(shell.Press("X"));
        await shell.Decision.Settled;

        Assert.NotEmpty(shell.Decision.Refusal);
        Assert.True(shell.IsSheetOpen);
        _ = Assert.Single(_server.DecideCalls);
    }, Token);

    [Fact]
    public Task A_row_that_clears_while_the_sheet_is_open_disables_the_form_and_keeps_its_text() => UiThread.Run(async () =>
    {
        var shell = await WithDecision();
        shell.Navigate(new DeepLink(DeepLinkKind.Decision, "DEC-1"));
        shell.Decision.Note = "half written";

        shell.ShowNeedsYou(new NeedsYouFeed([], ""));

        Assert.True(shell.Decision.IsCleared);
        Assert.All(shell.Decision.Options, o => Assert.False(o.IsEnabled));
        Assert.Equal("half written", shell.Decision.Note);
        Assert.Equal("Re-scope: drop the iOS head from this item", shell.Decision.Question);
    }, Token);

    [Fact]
    public Task Accepted_shows_the_outcome_for_2_s_then_drops_it_and_keeps_the_form_closed() => UiThread.Run(async () =>
    {
        var shell = await WithDecision();
        shell.Navigate(new DeepLink(DeepLinkKind.Decision, "DEC-1"));
        await shell.Decision.Choose("accept");
        var shown = shell.Decision.Outcome;

        _clock.Advance(TimeSpan.FromMilliseconds(1999));
        Assert.Equal(shown, shell.Decision.Outcome);
        _clock.Advance(TimeSpan.FromMilliseconds(1));
        await shell.Decision.Expiry;

        Assert.NotEmpty(shown);
        Assert.Empty(shell.Decision.Outcome);
        Assert.All(shell.Decision.Options, o => Assert.False(o.IsEnabled));
    }, Token);

    // The golden's decided list, as if the decision was answered three minutes before the clock reads.
    private static RouteAnswer DecidedThreeMinutesAgo(string scenario)
    {
        var served = GoldenFiles.Node("decisions", scenario)["decisions"]![0]!;
        var body = GoldenFiles.Api("decisions", scenario)
            .Replace((string)served["id"]!, "DEC-1", StringComparison.Ordinal)
            .Replace("2026-01-01T00:00:00.0000000Z", "2026-10-11T11:57:00.0000000Z", StringComparison.Ordinal);
        return new RouteAnswer(System.Net.HttpStatusCode.OK, body);
    }

    [Fact]
    public Task A_voided_decision_shows_the_cleared_line_the_decided_list_serves() => UiThread.Run(async () =>
    {
        var shell = await WithDecision();
        _server.DecidedDecisions = DecidedThreeMinutesAgo("voided");
        shell.Navigate(new DeepLink(DeepLinkKind.Decision, "DEC-1"));

        shell.ShowNeedsYou(new NeedsYouFeed([], ""));
        await shell.Decision.Settled;

        Assert.Equal("voided: the item was closed", shell.Decision.ClearedLine);
        Assert.Contains(_server.DecisionQueries, q => q.Pending == false);
    }, Token);

    [Fact]
    public Task A_decision_answered_elsewhere_clears_to_one_calm_line_in_words() => UiThread.Run(async () =>
    {
        var shell = await WithDecision();
        _server.DecidedDecisions = DecidedThreeMinutesAgo("decided");
        shell.Navigate(new DeepLink(DeepLinkKind.Decision, "DEC-1"));

        shell.ShowNeedsYou(new NeedsYouFeed([], ""));
        await shell.Decision.Settled;

        Assert.Equal("Decided: accepted by you at the terminal, 3m ago", shell.Decision.ClearedLine);
    }, Token);

    [Fact]
    public Task A_stale_Decide_is_refused_with_one_line_that_has_no_id_and_no_actor() => UiThread.Run(async () =>
    {
        var shell = await WithDecision();
        _server.DecideAnswers["DEC-1"] = RouteAnswer.Problem("decisions-id", "decided-elsewhere");
        _server.DecidedDecisions = DecidedThreeMinutesAgo("decided");
        shell.Navigate(new DeepLink(DeepLinkKind.Decision, "DEC-1"));

        await shell.Decision.Choose("decline");
        shell.ShowNeedsYou(new NeedsYouFeed([], ""));
        await shell.Decision.Settled;

        Assert.Equal("Already decided: accepted by you at the terminal, 3m ago", shell.Decision.Refusal);
        Assert.Equal(string.Empty, shell.Decision.ClearedLine);   // the refusal already says it
    }, Token);

    [Fact]
    public Task The_sheet_is_titled_with_the_request_and_says_who_filed_it_when_and_for_which_item() => UiThread.Run(async () =>
    {
        var shell = await WithDecision();

        shell.Navigate(new DeepLink(DeepLinkKind.Decision, "DEC-1"));

        Assert.Equal("Re-scope: drop the iOS head from this item", shell.Decision.Heading);
        Assert.Equal(shell.Decision.Heading, shell.SheetTitle);
        Assert.Equal("Filed by an agent session \u00b7 26m ago \u00b7 on ITM-01JD2", shell.Decision.FiledLine);
    }, Token);

    [Fact]
    public Task Once_decided_the_sheet_stops_asking_and_keeps_what_was_decided() => UiThread.Run(async () =>
    {
        var shell = await WithDecision();
        shell.Navigate(new DeepLink(DeepLinkKind.Decision, "DEC-1"));
        Assert.True(shell.Decision.IsAsking);

        await shell.Decision.Choose("accept");
        _clock.Advance(TimeSpan.FromSeconds(3));
        await shell.Decision.Expiry;

        Assert.Equal((false, "Decided: Accept"), (shell.Decision.IsAsking, shell.Decision.DecidedLine));
    }, Token);

    [Fact]
    public Task A_cleared_decision_the_decided_list_does_not_hold_falls_back_to_a_plain_cleared_line() => UiThread.Run(async () =>
    {
        var shell = await WithDecision();
        shell.Navigate(new DeepLink(DeepLinkKind.Decision, "DEC-1"));

        shell.ShowNeedsYou(new NeedsYouFeed([], ""));
        await shell.Decision.Settled;

        Assert.Equal(Labels.DecisionClearedFallback, shell.Decision.ClearedLine);
    }, Token);
}
