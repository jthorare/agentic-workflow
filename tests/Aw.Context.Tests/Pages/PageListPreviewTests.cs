using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Pages;

namespace Aw.Context.Tests.Pages;

/// <summary>A pasted page list previewed against the map's pages: which blocks match, which name the same page twice, which the save would add.</summary>
public sealed class PageListPreviewTests
{
    private const string Week = "surface:route/GET:/schedule/week";

    private static PageRow ScheduleWeek() => new(
        PageId.From("schedule-week"), NodeKey.From(Week), "Schedule week", Repo: "aw-split-panes", Mark: "suggested", Code: true,
        Actions:
        [
            Action("submit", "Submit schedule"), Action("cancel", "Cancel schedule"), Action("approve", "Approve"), Action("reopen", "Reopen week"),
        ]);

    private static PageActionRow Action(string id, string name) =>
        new(PageActionId.From(id), NodeKey.From($"action:WeekPage.{id}"), name, "suggested", Code: true, Concepts: []);

    [Fact, Trait("Lane", "unit")]
    public void Two_blocks_naming_the_same_page_flag_the_second()
    {
        var blocks = PageListText.Parse("Schedule week\nSubmit schedule\n\n- Schedule weeks\n- Approve");

        var rows = PageListPreview.Of(blocks, [ScheduleWeek()], []);

        Assert.Equal(2, rows.Count);
        Assert.Equal(Week, rows[0].Match?.Key.Value);
        Assert.True(rows[0].Ticked);
        Assert.Null(rows[0].SameAs);
        Assert.Equal(Week, rows[1].Match?.Key.Value);
        Assert.Equal(1, rows[1].SameAs);
        Assert.False(rows[1].Ticked);
        Assert.Equal("Not added: Same page as block 1", rows[1].Reason);
    }
}
