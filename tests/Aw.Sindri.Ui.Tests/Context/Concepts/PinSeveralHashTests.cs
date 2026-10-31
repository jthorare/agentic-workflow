using System.Net;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Concepts;

/// <summary>Add several's ifHash (concept pins spec §3.6): the hash of the pins read the rows were matched against, never a fresher one, and never none.</summary>
public sealed class PinSeveralHashTests
{
    private static readonly MapRebuildView _idle = new("idle", Progress: null, RetryAt: null, Stale: false, Failed: null);

    [Fact, Trait("Lane", "unit")]
    public async Task The_write_carries_the_hash_the_sheet_opened_with_not_a_fresher_read()
    {
        var fixtures = new PinSeveralFixtures { OpenFileHash = "h1" };
        fixtures.Api.ContextPinsAnswer = _ => PinSeveralFixtures.Served(new PinsView(1, "api", "h2", "v1", [], _idle));
        var sheet = await fixtures.Read("Payments");

        await sheet.PinAllAsync(PinSeveralFixtures.Token);

        Assert.Equal("h1", Assert.Single(fixtures.Api.PinnedConcepts).IfHash);
        Assert.Empty(fixtures.Api.ContextPinsAsked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_pins_hash_the_button_is_disabled_and_the_line_says_to_reopen()
    {
        var fixtures = new PinSeveralFixtures { OpenFileHash = null };
        var sheet = await fixtures.Read("Payments");

        await sheet.PinAllAsync(PinSeveralFixtures.Token);

        Assert.False(sheet.CanSubmit);
        Assert.Equal("Couldn't read your pins just now. Close this and open it again.", sheet.Message);
        Assert.Empty(fixtures.Api.PinnedConcepts);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_clash_re_reads_the_pins_and_the_hash_together_and_the_retry_carries_the_new_hash()
    {
        var fixtures = new PinSeveralFixtures();
        fixtures.Api.PinConceptsAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-PROFILE-003", "the profile changed since you read it");
        fixtures.Api.ContextPinsAnswer = _ => PinSeveralFixtures.Served(new PinsView(1, "api", "h9", "v1", [PinSeveralFixtures.Pin("payments", "Payments")], _idle));
        var sheet = await fixtures.Read("Payments: money\nRefunds");

        await sheet.PinAllAsync(PinSeveralFixtures.Token);

        Assert.Equal("The settings changed while you were editing. Your changes are still here. Press Pin all and approve again.", sheet.Message);
        Assert.Equal("Updates your pin Payments", sheet.Rows[0].Note);
        Assert.True(sheet.Rows[1].Ticked);
        Assert.True(sheet.CanSubmit);

        fixtures.Api.PinConceptsAnswer = new RouteAnswer(HttpStatusCode.OK, $$"""{"accepted":{"commandId":"00000000-0000-0000-0000-000000000009","sequence":12},"jobId":"{{PinSeveralFixtures.JobId}}"}""");
        await sheet.PinAllAsync(PinSeveralFixtures.Token);

        Assert.Equal([PinSeveralFixtures.FileHash, "h9"], fixtures.Api.PinnedConcepts.Select(b => b.IfHash), StringComparer.Ordinal);
        Assert.Equal(["Refunds"], fixtures.Api.PinnedConcepts[1].Set!.Select(p => p.Name), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_clash_whose_re_read_fails_leaves_no_hash_and_the_button_disabled()
    {
        var fixtures = new PinSeveralFixtures();
        fixtures.Api.PinConceptsAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-PROFILE-003", "the profile changed since you read it");
        fixtures.Api.ContextPinsAnswer = _ => RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-001", "restarting");
        var sheet = await fixtures.Read("Payments");

        await sheet.PinAllAsync(PinSeveralFixtures.Token);

        Assert.False(sheet.CanSubmit);
        Assert.Equal("Couldn't read your pins just now. Close this and open it again.", sheet.Message);
    }
}
