using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Context.Whole;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>#1724 P2b-2: the page sheet fills from the map's page, writes one page in one profile write, and keeps what was typed when it can't (pages spec §3.6).</summary>
[Lane(TestLane.Unit)]
public sealed class PageSheetTests
{
    private static readonly ConceptChoice _selfScheduling = new("self-scheduling", "Self scheduling");
    private static readonly ConceptChoice _approval = new("approval-workflow", "Approval workflow");
    private const string WeekKey = "surface:screen/week";

    private static PageDraft Week() => new(
        Id: "schedule-week", Name: "Schedule week", Repo: "web", Surface: WeekKey, Owner: false,
        Actions: [new PageActionDraft(Id: null, Name: "Submit schedule", Action: "action:submit", Concepts: ["self-scheduling"]), new PageActionDraft(Id: null, Name: "Copy last week", Action: "action:copy", Concepts: [])]);

    private static async Task<(PageSheetViewModel Sheet, FakePageWrites Writes)> Opened(PageDraft? page = null, ProfileWaiting? profile = null, IReadOnlyList<string>? others = null)
    {
        var writes = new FakePageWrites();
        if (profile is not null)
        {
            writes.Profile = profile;
        }

        writes.Add(WeekKey, page ?? Week());
        var sheet = new PageSheetViewModel(writes, [_selfScheduling, _approval], others ?? [], "self-scheduling");
        await sheet.OpenPage(WeekKey, TestContext.Current.CancellationToken);
        return (sheet, writes);
    }

    [Fact]
    public async Task Name_this_page_prefills_name_actions_and_ticks()
    {
        var (sheet, _) = await Opened();

        Expect.Equal("Name this page", sheet.Title);
        Expect.Equal("Schedule week", sheet.Name);
        Expect.Sequence(["Submit schedule", "Copy last week"], sheet.Actions.Select(a => a.Name));
        Expect.Sequence([true], sheet.Actions[0].Ticks.Select(t => t.IsTicked));
        Expect.Sequence([false], sheet.Actions[1].Ticks.Select(t => t.IsTicked));
        Expect.Equal("Relates to Self scheduling", sheet.Actions[0].Ticks[0].AccessibleName);
        Expect.Equal("Action 1 of 2, Submit schedule, relates to 1 concept", sheet.Actions[0].RowName);
        Expect.Equal("From the map: week", sheet.FromMap);
        Expect.Equal("Save and approve", sheet.SaveText);
        Expect.True(!sheet.HasChanges, "what the map prefilled is no change to discard");
    }

    [Fact]
    public async Task Ticking_a_second_concept_saves_both()
    {
        var (sheet, writes) = await Opened();
        sheet.SelectedRelate = _approval;
        sheet.Actions[0].Ticks[1].IsTicked = true;
        var closed = false;
        sheet.Closed += (_, _) => closed = true;

        await sheet.Save(TestContext.Current.CancellationToken);

        var call = Assert.Single(writes.Saves);
        Expect.Sequence(["self-scheduling", "approval-workflow"], call.Save.Set[0].Actions[0].Concepts);
        Expect.Sequence([], call.Save.Set[0].Actions[1].Concepts);
        Expect.Equal("hash-1", call.IfHash);
        Expect.True(call.Approve, "only this page waits, so the app approves");
        Expect.True(closed, "a saved page closes the sheet");
        Expect.Equal("schedule-week", sheet.SavedId);
    }

    [Fact]
    public async Task Add_an_action_and_a_page_with_no_actions_saves()
    {
        var (sheet, writes) = await Opened();
        sheet.AddAction();
        sheet.Actions[2].Name = "  Print the week ";
        sheet.AddAction();   // left empty: dropped
        await sheet.Save(TestContext.Current.CancellationToken);

        Expect.Sequence(["Submit schedule", "Copy last week", "Print the week"], writes.Saves[0].Save.Set[0].Actions.Select(a => a.Name));

        var bare = new FakePageWrites();
        var empty = new PageSheetViewModel(bare, [_selfScheduling], [], openConcept: null);
        await empty.OpenNew(TestContext.Current.CancellationToken);
        Expect.Equal("New page", empty.Title);
        Expect.Equal(1, empty.Actions.Count);
        empty.Name = " Time off ";
        await empty.Save(TestContext.Current.CancellationToken);

        var page = bare.Saves[0].Save.Set[0];
        Expect.Equal("Time off", page.Name);
        Expect.True(page.Id is null, "a new page sends no id: the server makes one");
        Expect.Equal(0, page.Actions.Count);
    }

    [Fact]
    public async Task New_page_sends_no_id_and_never_overwrites()
    {
        var writes = new FakePageWrites { Profile = new ProfileWaiting("h", ["index.pages.time-off"]) };
        var sheet = new PageSheetViewModel(writes, [], [], openConcept: null);
        await sheet.OpenNew(TestContext.Current.CancellationToken);
        sheet.Name = "Time off";

        await sheet.Save(TestContext.Current.CancellationToken);

        var call = Assert.Single(writes.Saves);
        Expect.True(call.Save.Set[0].Id is null, "the draft carries no id, so no existing page is replaced");
        Expect.Sequence(["index.pages.time-off"], call.Save.Before);
        Expect.True(!call.Approve, "another page already waits, so a new page is only saved");
    }

    [Fact]
    public async Task The_button_reads_Save_when_another_change_waits()
    {
        var (mine, _) = await Opened(profile: new ProfileWaiting("h", ["index.pages.schedule-week"]));
        var (other, otherWrites) = await Opened(profile: new ProfileWaiting("h", ["index.pages.schedule-week", "repos.web.index.pages.other"]));
        var unreadable = new FakePageWrites { Profile = null };
        unreadable.Add(WeekKey, Week());
        var blind = new PageSheetViewModel(unreadable, [], [], openConcept: null);
        await blind.OpenPage(WeekKey, TestContext.Current.CancellationToken);

        Expect.Equal("Save and approve", mine.SaveText);
        Expect.Equal("Save", other.SaveText);
        Expect.Equal("Save", blind.SaveText);
        await other.Save(TestContext.Current.CancellationToken);
        Expect.True(!otherWrites.Saves[0].Approve, "a write beside other waiting changes is not approved");
    }

    [Fact]
    public async Task A_clash_keeps_every_field()
    {
        var (sheet, writes) = await Opened();
        sheet.Name = "Week schedule";
        sheet.Actions[1].Name = "Copy";
        writes.Answer(new PageSaved(PageSavedState.Clash, "SND-PROFILE-003"));
        writes.Profile = new ProfileWaiting("hash-2", []);
        var closed = false;
        sheet.Closed += (_, _) => closed = true;

        await sheet.Save(TestContext.Current.CancellationToken);

        Expect.Equal("The profile changed since you opened this. Your changes are kept: Save again to apply them on top.", sheet.Note);
        Expect.True(!closed, "a clash leaves the sheet open");
        Expect.Equal("Week schedule", sheet.Name);
        Expect.Equal("Copy", sheet.Actions[1].Name);
        await sheet.Save(TestContext.Current.CancellationToken);
        Expect.Equal("hash-2", writes.Saves[1].IfHash);
        Expect.True(closed, "the second save lands");
    }

    [Fact]
    public async Task A_hand_edit_before_the_sheet_opened_saves_on_the_file_hash()
    {
        var (sheet, writes) = await Opened(profile: new ProfileWaiting("live-old", [], FileHash: "file-new"));
        sheet.Name = "Week schedule";

        await sheet.Save(TestContext.Current.CancellationToken);

        Expect.Equal("file-new", Assert.Single(writes.Saves).IfHash);
    }

    [Fact]
    public async Task A_hand_edit_after_the_sheet_opened_still_clashes()
    {
        var (sheet, writes) = await Opened(profile: new ProfileWaiting("live-old", [], FileHash: "file-new"));
        sheet.Name = "Week schedule";
        writes.Profile = new ProfileWaiting("live-old", [], FileHash: "file-newer");

        await sheet.Save(TestContext.Current.CancellationToken);

        Expect.Equal("file-new", Assert.Single(writes.Saves).IfHash);
    }

    [Fact]
    public async Task A_write_still_running_says_so_and_never_reads_as_failed()
    {
        var (sheet, writes) = await Opened();
        writes.Answer(new PageSaved(PageSavedState.Failed, Message: "The write is still running. Check Jobs, then try again."));

        await sheet.Save(TestContext.Current.CancellationToken);

        Expect.Equal("The write is still running. Check Jobs, then try again.", sheet.Note);
    }

    [Fact]
    public async Task A_failed_approve_after_the_write_landed_closes_as_waiting()
    {
        var (sheet, writes) = await Opened();
        writes.Answer(new PageSaved(PageSavedState.Waiting, "SND-API-001", "The control plane is restarting."));
        var closed = false;
        sheet.Closed += (_, _) => closed = true;

        await sheet.Save(TestContext.Current.CancellationToken);

        Expect.True(closed, "the write landed: the sheet closes and the row waits for approval");
        Expect.Equal(string.Empty, sheet.Note);
    }

    [Fact]
    public async Task A_refused_concept_shows_under_its_action()
    {
        var (sheet, writes) = await Opened();
        writes.Answer(new PageSaved(PageSavedState.Refused, "SND-CONTEXT-250", "index.pages[0].actions[1].concepts[0]: 'aproval' is no concept in map v41 (fix: try approval-workflow)"));

        await sheet.Save(TestContext.Current.CancellationToken);

        Expect.Equal("'aproval' is no concept in map v41 (fix: try approval-workflow)", sheet.Actions[1].Error);
        Expect.Equal(string.Empty, sheet.Actions[0].Error);
        Expect.Equal(string.Empty, sheet.NameError);
        writes.Answer(new PageSaved(PageSavedState.Refused, "SND-CONTEXT-255", "index.pages[0].name: 81 characters; at most 80"));
        await sheet.Save(TestContext.Current.CancellationToken);
        Expect.Equal("81 characters; at most 80", sheet.NameError);
        Expect.Equal(string.Empty, sheet.Actions[1].Error);
        writes.Answer(new PageSaved(PageSavedState.Refused, "SND-CONTEXT-253", "index.pages: 201 pages; at most 200"));
        await sheet.Save(TestContext.Current.CancellationToken);
        Expect.Equal("index.pages: 201 pages; at most 200", sheet.Note);
        writes.Answer(new PageSaved(PageSavedState.Failed, "SND-JOB-001"));
        await sheet.Save(TestContext.Current.CancellationToken);
        Expect.Equal("Couldn't save just now. Try again.", sheet.Note);
    }

    [Fact]
    public async Task Remove_page_asks_with_focus_on_Cancel()
    {
        var owner = Week() with { Owner = true };
        var (sheet, writes) = await Opened(owner);
        var onCancel = 0;
        var back = 0;
        sheet.CancelRemoveFocusRequested += (_, _) => onCancel++;
        sheet.RemovePageFocusRequested += (_, _) => back++;

        Expect.Equal("Edit page", sheet.Title);
        Expect.True(sheet.ShowRemovePage, "Edit page has Remove page");
        sheet.AskRemove();
        Expect.True(sheet.IsConfirmingRemove, "Remove page asks first");
        Expect.Equal(1, onCancel);
        Expect.Equal("Remove the page Schedule week? Its actions and their concept links go too; the map's own page stays as a suggestion.", sheet.RemoveQuestion);
        Expect.True(sheet.Escape(), "Esc is spent on the question");
        Expect.True(!sheet.IsConfirmingRemove && writes.Saves.Count == 0, "Esc cancels and writes nothing");
        Expect.Equal(1, back);

        sheet.AskRemove();
        await sheet.Remove(TestContext.Current.CancellationToken);
        var call = Assert.Single(writes.Saves);
        Expect.Equal(0, call.Save.Set.Count);
        Expect.Sequence(["schedule-week"], call.Save.Remove);
        Expect.True(call.Approve, "the approve rule holds for a removal too");

        var (suggested, _) = await Opened();
        Expect.True(!suggested.ShowRemovePage, "a suggested page has nothing to remove");
    }

    [Fact]
    public async Task Esc_with_changes_asks_to_discard()
    {
        var (sheet, _) = await Opened();
        var closed = 0;
        var keep = 0;
        sheet.Closed += (_, _) => closed++;
        sheet.KeepEditingFocusRequested += (_, _) => keep++;

        sheet.Name = "Week";
        _ = sheet.Escape();
        Expect.True(sheet.IsConfirmingDiscard && closed == 0 && keep == 1, "Esc with a change asks and leaves the sheet open");
        _ = sheet.Escape();
        Expect.True(!sheet.IsConfirmingDiscard && closed == 0, "Esc on the question is Keep editing");
        _ = sheet.Escape();
        sheet.Close();
        Expect.Equal(1, closed);

        var (quiet, _) = await Opened();
        var quietClosed = false;
        quiet.Closed += (_, _) => quietClosed = true;
        _ = quiet.Escape();
        Expect.True(quietClosed && !quiet.IsConfirmingDiscard, "without changes Esc just closes");
    }

    [Fact]
    public async Task Another_page_with_the_same_name_warns_but_does_not_block()
    {
        var (sheet, writes) = await Opened(others: ["time-off request", "schedule WEEK"]);

        Expect.True(sheet.HasSameName, "the name matches another row, ignoring case");
        Expect.Equal("Another page is called Schedule week", sheet.SameNameText);
        await sheet.Save(TestContext.Current.CancellationToken);
        Expect.Equal(1, writes.Saves.Count);
    }

    [Fact]
    public void A_saved_row_wears_Waiting_for_approval_once_the_profile_says_it_waits()
    {
        var writes = new FakePageWrites { Profile = new ProfileWaiting("h", ["index.pages.schedule-week"]) };
        var section = new PagesSectionViewModel { Writes = writes };
        section.Remember(WeekKey, "schedule-week");

        section.Show(PagesSectionTests.Scheduling());

        var week = section.Rows.Single(r => r.Key == WeekKey);
        Expect.Sequence(["Suggested", "Code found", "Waiting for approval"], week.Marks);
        Expect.True(week.CanWrite && !section.Rows[0].Marks.Contains("Waiting for approval", StringComparer.Ordinal), "only the saved row waits");
        Expect.Equal("Name this page", week.ButtonText);
        Expect.Equal("Edit page", section.Rows[0].ButtonText);
    }
}
