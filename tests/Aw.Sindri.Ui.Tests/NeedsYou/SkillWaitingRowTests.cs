using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>The Needs you row of a waiting skill block (skill flows spec §5.4): the SKILL chip, one line of text, Details, Take over and the one-time hint.</summary>
[Lane(TestLane.Unit)]
public sealed class SkillWaitingRowTests
{
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static T Golden<T>(string slug, string scenario)
        where T : class =>
        AwJson.Deserialize<T>(Encoding.UTF8.GetBytes(GoldenFiles.Api(slug, scenario)), SindriApiClient.Json).Value;

    private static NeedsYouEntry Entry(Func<OwnerWaitingView, OwnerWaitingView>? change = null, string? reason = null)
    {
        var list = Golden<NeedsYouList>("needs-you", "skill-waiting");
        var row = list.Rows[0];
        row = row with { OwnerWaiting = change?.Invoke(row.OwnerWaiting!) ?? row.OwnerWaiting, Reason = reason ?? row.Reason };
        return NeedsYouMapper.Map(list with { Rows = [row] }, Golden<CommandList>("commands", "catalog"), decisions: null, new HomeCounts(Running: 0, WithYou: 0, Queued: 0), readAt: A.Noon).Rows[0];
    }

    private NeedsYouRowViewModel Row(NeedsYouEntry entry) => new(entry, seen: false, reducedMotion: false, arrived: false, _clock);

    [Fact]
    public void The_mapper_gives_the_row_a_SKILL_chip_and_carries_what_the_skill_waits_on()
    {
        var entry = Entry();

        Assert.Equal(new StateChip("SKILL", ChipTone.Attn), entry.Chip);
        Assert.Equal(new OwnerWaitingView("LCH-00000000000000000000000000000001", "question", "Who calls this API?", "/run/.sindri-out/result-1.json", Detail: null, "ITM-1/step/1", "review", Attempt: 1), entry.OwnerWaiting);
    }

    [Fact]
    public void A_row_of_another_kind_carries_no_ownerWaiting()
    {
        var list = Golden<NeedsYouList>("needs-you", "fixture");

        var feed = NeedsYouMapper.Map(list, Golden<CommandList>("commands", "catalog"), decisions: null, new HomeCounts(Running: 0, WithYou: 0, Queued: 0));

        Assert.All(feed.Rows, r => Assert.Null(r.OwnerWaiting));
    }

    [Fact]
    public void The_golden_row_offers_Take_over_as_its_primary_button()
    {
        var row = Row(Entry());

        var primary = Assert.Single(row.Controls);
        Assert.Equal(("Take over", true), (primary.Action.Label, primary.IsPrimary));
        Assert.True(primary.IsEnabled);
    }

    [Fact]
    public void The_row_text_is_the_served_reason()
    {
        var row = Row(Entry());

        Assert.True(row.IsSkillWaiting);
        Assert.Equal("Shape the idea is waiting on you: Who calls this API?", row.SkillLine);
    }

    [Fact]
    public void Details_open_the_full_question_the_result_path_and_the_detail()
    {
        var row = Row(Entry(w => w with { Detail = "office-hours was updated" }));

        Assert.True(row.CanShowSkillDetails);
        Assert.False(row.SkillDetailsOpen);
        Assert.Equal("Details", row.SkillDetailsButtonText);

        row.ToggleSkillDetails();

        Assert.True(row.SkillDetailsOpen);
        Assert.Equal("Hide details", row.SkillDetailsButtonText);
        Assert.Equal(("Who calls this API?", "/run/.sindri-out/result-1.json", "office-hours was updated"), (row.SkillQuestion, row.SkillResultPath, row.SkillDetail));

        row.ToggleSkillDetails();
        Assert.False(row.SkillDetailsOpen);
    }

    [Fact]
    public void A_noResult_row_has_no_Details()
    {
        var row = Row(Entry(w => w with { Reason = "noResult", Text = null }, reason: "Shape the idea ended without a result."));

        Assert.False(row.CanShowSkillDetails);
        row.ToggleSkillDetails();
        Assert.False(row.SkillDetailsOpen);
    }

    [Theory]
    [InlineData("unreadable")]
    [InlineData("skillUpdated")]
    public void The_other_reasons_keep_Details_and_show_no_question(string reason)
    {
        var row = Row(Entry(w => w with { Reason = reason, Text = null, Detail = "x" }));

        Assert.True(row.CanShowSkillDetails);
        Assert.Equal("", row.SkillQuestion);
    }

    [Fact]
    public void A_row_of_another_kind_is_not_a_skill_row()
    {
        var row = Row(NeedsYouRows.Desk());

        Assert.False(row.IsSkillWaiting);
        Assert.False(row.CanShowSkillDetails);
        Assert.Equal("", row.SkillLine);
    }

    private static NeedsYouEntry Waiting(string id) => Entry() with { Id = id };

    private async Task<(NeedsYouViewModel List, Preferences Preferences)> Open(InMemoryPreferencesStore store)
    {
        var preferences = new Preferences(store);
        var list = new NeedsYouViewModel(preferences, new FakeMotionPreference(), _clock);
        await list.Restore(Token);
        return (list, preferences);
    }

    [Fact]
    public async Task Only_the_first_waiting_row_the_owner_ever_meets_carries_the_hint()
    {
        var (list, _) = await Open(new InMemoryPreferencesStore());

        list.Update(new NeedsYouFeed([Waiting("skill:a"), Waiting("skill:b")], ""));

        Assert.Equal([true, false], list.Rows.Select(r => r.ShowsTakeoverHint));
    }

    [Fact]
    public async Task The_hint_is_remembered_across_restarts_and_never_moves_to_another_row()
    {
        var store = new InMemoryPreferencesStore();
        var (first, written) = await Open(store);
        first.Update(new NeedsYouFeed([Waiting("skill:a")], ""));
        await written.Flush();

        var (next, _) = await Open(store);
        next.Update(new NeedsYouFeed([Waiting("skill:a"), Waiting("skill:b")], ""));
        Assert.Equal([true, false], next.Rows.Select(r => r.ShowsTakeoverHint));

        var (later, _) = await Open(store);
        later.Update(new NeedsYouFeed([Waiting("skill:b")], ""));
        Assert.Equal([false], later.Rows.Select(r => r.ShowsTakeoverHint));
    }

    [Fact]
    public async Task A_row_that_is_not_a_skill_row_never_takes_the_hint()
    {
        var (list, _) = await Open(new InMemoryPreferencesStore());

        list.Update(new NeedsYouFeed([NeedsYouRows.Desk(), Waiting("skill:a")], ""));

        Assert.Equal([false, true], list.Rows.Select(r => r.ShowsTakeoverHint));
    }
}
