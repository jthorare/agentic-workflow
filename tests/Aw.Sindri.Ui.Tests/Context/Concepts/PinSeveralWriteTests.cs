using System.Net;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Concepts;

/// <summary>Add several's write (concept pins spec §3.6 "The write", "Sheet states"): one PinConcepts job for every ticked row, then the shared approve flow.</summary>
public sealed class PinSeveralWriteTests
{
    private static async Task<(PinSeveralFixtures Fixtures, PinSeveralViewModelBox Box)> Ready(string list, PinSeveralFixtures? given = null)
    {
        var fixtures = given ?? new PinSeveralFixtures();
        var closed = new PinSeveralViewModelBox();
        var sheet = await fixtures.Read(list, () => closed.Closed++);
        closed.Sheet = sheet;
        return (fixtures, closed);
    }

    private static string Show(PinSetBody pin) =>
        string.Join('|', pin.Name, pin.Id ?? "-", pin.Summary ?? "-", pin.Aliases is null ? "-" : string.Join(',', pin.Aliases), pin.Features is null ? "-" : string.Join(',', pin.Features));

    [Fact, Trait("Lane", "unit")]
    public async Task The_button_says_pin_all_and_approve_when_nothing_else_waits_and_pin_all_when_something_does()
    {
        var (_, free) = await Ready("Payments");
        var waiting = new PinSeveralFixtures(pins: null, "repos.api.index.concepts.auth");
        var other = waiting.Sheet();
        await other.StartAsync(PinSeveralFixtures.Token);

        Assert.Equal("Pin all and approve", free.Sheet!.SubmitText);
        Assert.Equal("Pin all", other.SubmitText);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_no_row_ticked_the_button_is_disabled_and_says_to_tick_one()
    {
        var (_, box) = await Ready("Payments");

        Assert.Equal(string.Empty, box.Sheet!.SubmitNote);
        box.Sheet.Rows[0].Ticked = false;

        Assert.False(box.Sheet.CanSubmit);
        Assert.Equal("Tick at least one line.", box.Sheet.SubmitNote);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Pin_all_sends_one_write_with_every_ticked_row_and_the_pins_file_hash()
    {
        var fixtures = new PinSeveralFixtures([PinSeveralFixtures.Pin("billing", "Billing")]);
        fixtures.Matches["Payments"] = "concept:refunds";
        fixtures.Concepts["concept:refunds"] = PinSeveralFixtures.WithFeatures([new("refund", 3)], new PinSuggestFeature("feature:refunds", "Refunds"), new PinSuggestFeature("feature:chargebacks", "Chargebacks"));
        var (_, box) = await Ready("Payments: money (also: ledger)\nBilling: plans (also: invoicing)\nSync\nNotify", fixtures);
        box.Sheet!.Rows[1].Ticked = true;
        box.Sheet.Rows[3].Ticked = false;

        await box.Sheet.PinAllAsync(PinSeveralFixtures.Token);

        var body = Assert.Single(fixtures.Api.PinnedConcepts);
        Assert.Equal(PinSeveralFixtures.FileHash, body.IfHash);
        Assert.Equal(
            [
                "Payments|-|money|ledger,refund|feature:refunds,feature:chargebacks",
                "Billing|billing|plans|invoicing|-",
                "Sync|-|-||",
            ],
            body.Set!.Select(Show),
            StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_button_reads_pinning_with_the_count_while_the_write_runs()
    {
        var (fixtures, box) = await Ready("Payments\nRefunds\nSync");
        var seen = new List<string>();
        box.Sheet!.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(box.Sheet.SubmitText), StringComparison.Ordinal))
            {
                seen.Add(box.Sheet.SubmitText);
            }
        };

        await box.Sheet.PinAllAsync(PinSeveralFixtures.Token);

        Assert.Contains("Pinning 3…", seen, StringComparer.Ordinal);
        _ = Assert.Single(fixtures.Api.PinnedConcepts);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_write_that_lands_and_is_approved_closes_the_sheet_and_approves_the_jobs_hash()
    {
        var fixtures = new PinSeveralFixtures();
        fixtures.Api.ProfileAnswer = PinSeveralFixtures.Profile("h1", "index.concepts.payments", "repos.api.index.concepts.refunds");
        var (_, box) = await Ready("Payments\nRefunds", fixtures);

        await box.Sheet!.PinAllAsync(PinSeveralFixtures.Token);

        Assert.Equal(1, box.Closed);
        Assert.Equal("h1", Assert.Single(fixtures.Api.ProfileApprovals).Hash);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_write_that_lands_beside_other_waiting_changes_closes_without_approving()
    {
        var fixtures = new PinSeveralFixtures(pins: null, "index.pages.schedule-week");
        fixtures.Api.ProfileAnswer = PinSeveralFixtures.Profile("h1", "index.pages.schedule-week", "index.concepts.payments");
        var (_, box) = await Ready("Payments", fixtures);

        await box.Sheet!.PinAllAsync(PinSeveralFixtures.Token);

        Assert.Equal(1, box.Closed);
        Assert.Empty(fixtures.Api.ProfileApprovals);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_version_clash_keeps_every_row_and_says_to_press_the_button_again()
    {
        var fixtures = new PinSeveralFixtures();
        fixtures.Api.PinConceptsAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-PROFILE-003", "the profile changed since you read it");
        var (_, box) = await Ready("Payments: money\nRefunds", fixtures);
        box.Sheet!.Rows[1].Ticked = false;
        box.Sheet.Rows[0].Definition = "typed since";

        await box.Sheet.PinAllAsync(PinSeveralFixtures.Token);

        Assert.Equal("The settings changed while you were editing. Your changes are still here. Press Pin all and approve again.", box.Sheet.Message);
        Assert.Equal(0, box.Closed);
        Assert.Equal("typed since", box.Sheet.Rows[0].Definition);
        Assert.False(box.Sheet.Rows[1].Ticked);
        Assert.True(box.Sheet.CanSubmit);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_file_that_changed_since_the_pins_read_is_refused_with_the_hash_that_was_read_and_everything_is_kept()
    {
        var fixtures = new PinSeveralFixtures();
        fixtures.Api.ContextPinsAnswer = _ => PinSeveralFixtures.Served(new PinsView(1, "api", "h1", "v1", [], new("idle", Progress: null, RetryAt: null, Stale: false, Failed: null)));
        fixtures.Api.PinConceptsAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-PROFILE-003", "the profile changed since you read it");
        var (_, box) = await Ready("Payments: money\nRefunds: back\nSync", fixtures);
        box.Sheet!.Rows[0].Name = "Card payments";
        box.Sheet.Rows[1].Definition = "typed since";
        box.Sheet.Rows[2].Ticked = false;

        await box.Sheet.PinAllAsync(PinSeveralFixtures.Token);

        Assert.Equal("h1", Assert.Single(fixtures.Api.PinnedConcepts).IfHash);
        Assert.Equal("The settings changed while you were editing. Your changes are still here. Press Pin all and approve again.", box.Sheet.Message);
        Assert.Equal(0, box.Closed);
        Assert.Equal("Card payments", box.Sheet.Rows[0].Name);
        Assert.Equal("typed since", box.Sheet.Rows[1].Definition);
        Assert.Equal([true, true, false], box.Sheet.Rows.Select(r => r.Ticked));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_refused_write_shows_the_servers_sentence_and_stays_open()
    {
        var fixtures = new PinSeveralFixtures();
        fixtures.Api.PinConceptsAnswer = RouteAnswer.Problem(HttpStatusCode.BadRequest, "SND-CONTEXT-171", "index.concepts[2].name: too long");
        var (_, box) = await Ready("Payments", fixtures);

        await box.Sheet!.PinAllAsync(PinSeveralFixtures.Token);

        Assert.Contains("index.concepts[2].name: too long", box.Sheet.Message, StringComparison.Ordinal);
        Assert.Equal(0, box.Closed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_write_still_running_after_the_wait_says_so_in_the_sheets_own_sentence()
    {
        var fixtures = new PinSeveralFixtures();
        fixtures.Api.JobAnswers[PinSeveralFixtures.JobId] = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-001", "The control plane is restarting.");
        var (_, box) = await Ready("Payments", fixtures);

        await box.Sheet!.PinAllAsync(PinSeveralFixtures.Token);

        Assert.Equal("The pin write is still running. Check Jobs, then try again.", box.Sheet.Message);
        Assert.Equal(0, box.Closed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_pins_hash_comes_from_one_swappable_read_made_once_at_open()
    {
        var fixtures = new PinSeveralFixtures();
        var reads = 0;
        var sheet = fixtures.Sheet(hash: _ =>
        {
            reads++;
            return Task.FromResult<string?>("swapped");
        });
        await sheet.StartAsync(PinSeveralFixtures.Token);
        sheet.Text = "Payments";
        await sheet.ReadListAsync(PinSeveralFixtures.Token);

        await sheet.PinAllAsync(PinSeveralFixtures.Token);

        Assert.Equal("swapped", Assert.Single(fixtures.Api.PinnedConcepts).IfHash);
        Assert.Equal(1, reads);
    }
}
