using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Projects;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Projects;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>The notes area of a Projects card (projects page spec §3, §5.1): the counter and its limit, Save and what it sends, the re-read after an accepted write, the 409, Cancel.</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectNotesTests
{
    private const string Led = "linear:project/00000000-0000-4000-8000-000000000001";

    private readonly ProjectsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ProjectCardViewModel> LedCard() => (await _f.Shown(Token)).Groups[0].Cards[0];

    private async Task<ProjectCardViewModel> HandMadeCard() => (await _f.Shown(Token)).Groups[1].Cards[0];

    // What GET /sindri/projects/{id} answers once the write is projected: the attached golden under this id with new notes and the next version.
    private void ReadsBack(ProjectCardViewModel card, string? notes, long version)
    {
        var one = GoldenFiles.Node("projects-id", "attached");
        one["project"]!["id"] = card.Id;
        one["project"]!["name"] = card.Name;
        one["project"]!["notes"] = notes;
        one["project"]!["version"] = version;
        _f.Server.ProjectAnswers[card.Id] = new RouteAnswer(HttpStatusCode.OK, one.ToJsonString());
    }

    [Theory]
    [InlineData(0, "0 / 16,384")]
    [InlineData(1204, "1,204 / 16,384")]
    [InlineData(16_384, "16,384 / 16,384")]
    public async Task The_counter_counts_with_a_thousands_separator(int length, string counter)
    {
        var card = await LedCard();
        card.BeginEdit();

        card.Draft = new string('a', length);

        Assert.Equal(counter, card.Counter);
    }

    [Theory]
    [InlineData(16_384, true)]
    [InlineData(16_385, false)]
    public async Task Save_is_off_over_the_limit(int length, bool canSave)
    {
        var card = await LedCard();
        card.BeginEdit();

        card.Draft = new string('a', length);

        Assert.Equal((canSave, !canSave), (card.CanSave, card.OverLimit));
    }

    [Fact]
    public async Task Editing_starts_from_the_notes_shown_and_the_card_shows_them_whole()
    {
        var card = await _f.FirstCard(c => c["notes"] = "line one\nline two", Token);

        Assert.Equal(("line one\nline two", true, false), (card.NotesText, card.HasNotes, card.IsEditing));

        card.BeginEdit();

        Assert.Equal(("line one\nline two", true), (card.Draft, card.IsEditing));
    }

    [Fact]
    public async Task Save_sends_the_version_the_card_was_opened_at_and_the_typed_notes()
    {
        var card = await LedCard();
        ReadsBack(card, "ship it", 1);
        card.BeginEdit();
        card.Draft = "ship it";

        await card.Save(Token);

        var (route, body) = Assert.Single(_f.Server.ProjectWrites);
        Assert.Equal((ProjectRoutes.Change(Led), new ProjectChange(0, Notes: "ship it")), (route, body));
    }

    [Fact]
    public async Task A_led_card_writes_under_its_linear_key_and_a_hand_made_one_under_its_prj_id()
    {
        var led = await LedCard();
        var kitchen = await HandMadeCard();
        ReadsBack(led, "a", 1);
        ReadsBack(kitchen, "b", 1);
        led.BeginEdit();
        led.Draft = "a";
        kitchen.BeginEdit();
        kitchen.Draft = "b";

        await led.Save(Token);
        await kitchen.Save(Token);

        Assert.Equal([ProjectRoutes.Change(Led), ProjectRoutes.Change(kitchen.Id)], _f.Server.ProjectWrites.Select(w => w.Route), StringComparer.Ordinal);
        Assert.StartsWith("prj_", kitchen.Id, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Emptying_the_box_clears_the_notes()
    {
        var card = await _f.FirstCard(c => c["notes"] = "old", Token);
        ReadsBack(card, notes: null, 1);
        card.BeginEdit();
        card.Draft = "";

        await card.Save(Token);

        Assert.Equal(new ProjectChange(0, ClearNotes: true), Assert.Single(_f.Server.ProjectWrites).Body);
        Assert.False(card.HasNotes);
    }

    [Fact]
    public async Task After_a_save_the_card_shows_what_the_API_kept_not_what_was_typed()
    {
        var card = await LedCard();
        ReadsBack(card, "token [redacted]", 1);
        card.BeginEdit();
        card.Draft = "token ghp_secret";

        await card.Save(Token);

        Assert.Equal([Led], _f.Server.ProjectsRead);
        Assert.Equal(("token [redacted]", 1L, false, null), (card.NotesText, card.Version, card.IsEditing, card.SaveError));
        Assert.Equal(("Notes saved for Checkout rewrite", true), (card.Voice.Announcement, card.Voice.IsAnnouncing));
    }

    [Fact]
    public async Task Save_says_Saving_while_the_write_is_in_flight()
    {
        var card = await LedCard();
        ReadsBack(card, "x", 1);
        _f.Server.CommandGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        card.BeginEdit();
        card.Draft = "x";

        var saving = card.Save(Token);

        Assert.Equal(("Saving…", true, false), (card.SaveText, card.IsSaving, card.CanSave));
        _f.Server.CommandGate.SetResult();
        await saving;
        Assert.Equal(("Save", false), (card.SaveText, card.IsSaving));
    }

    [Fact]
    public async Task A_409_keeps_the_typed_text_until_Reload_replaces_it()
    {
        var card = await LedCard();
        _f.Server.ProjectWriteAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-PROJECT-409", "The project changed.");
        card.BeginEdit();
        card.Draft = "mine";

        await card.Save(Token);

        Assert.Equal((true, true, "mine", true), (card.IsConflict, card.IsEditing, card.Draft, card.CanSave));
        Assert.Empty(_f.Server.ProjectsRead);

        ReadsBack(card, "theirs", 3);
        await card.Reload(Token);

        Assert.Equal((false, "theirs", 3L, "theirs"), (card.IsConflict, card.Draft, card.Version, card.NotesText));
        Assert.True(card.IsEditing);
        Assert.Equal([Led], _f.Server.ProjectsRead);
    }

    [Fact]
    public async Task Another_refusal_shows_under_Save_and_keeps_the_text()
    {
        var card = await LedCard();
        _f.Server.ProjectWriteAnswer = RouteAnswer.Problem(HttpStatusCode.BadRequest, "SND-PROJECT-004", "notes are too long.");
        card.BeginEdit();
        card.Draft = "mine";

        await card.Save(Token);

        Assert.Equal((false, true, "mine"), (card.IsConflict, card.IsEditing, card.Draft));
        Assert.Contains("SND-PROJECT-004", card.SaveError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancel_restores_the_notes_and_leaves_nothing_sent()
    {
        var card = await _f.FirstCard(c => c["notes"] = "kept", Token);
        card.BeginEdit();
        card.Draft = "scratch";

        card.CancelEdit();

        Assert.Equal((false, "kept", "kept"), (card.IsEditing, card.NotesText, card.Draft));
        Assert.Empty(_f.Server.ProjectWrites);
    }

    [Fact]
    public async Task A_newer_serving_while_editing_keeps_the_draft()
    {
        var card = await _f.FirstCard(c => c["notes"] = "kept", Token);
        card.BeginEdit();
        card.Draft = "half typed";
        var served = ProjectsFixture.Golden("mixed");
        served["projects"]![0]!["notes"] = "other";
        served["projects"]![0]!["version"] = 5;
        _f.Serve(served);

        _ = await _f.Shown(Token);   // a second page; the card under test is updated by the page that owns it
        card.Update(card.Card with { Notes = "other", Version = 5 });

        Assert.Equal(("half typed", "other"), (card.Draft, card.NotesText));
    }
}
