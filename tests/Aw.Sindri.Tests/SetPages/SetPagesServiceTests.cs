using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.SetPages;

namespace Aw.Sindri.Tests.SetPages;

/// <summary>The SetPages job (pages spec §3.1, "The write"): it writes pages into 'index.pages', returns the new hash and never approves; every refusal comes before the write.</summary>
[Lane(TestLane.Unit)]
public sealed class SetPagesServiceTests : IDisposable
{
    private readonly SetPagesRig _rig = new();

    public void Dispose() => _rig.Dispose();

    [Fact]
    public async Task Page_set_replaces_the_page_and_waits_for_approval()
    {
        _rig.Profile(
            [new PageSettings(PagePinId.From("schedule-week"), "Schedule wk", Actions: [new PageActionSettings(PageActionPinId.From("old"), "Old")])],
            new RepoEntry("web", "/work/web"));
        var page = new PageSettings(
            PagePinId.From("schedule-week"),
            "Schedule week",
            Surface: "surface:route/GET:/schedule/week",
            Actions: [new PageActionSettings(Name: "Submit a schedule", Action: "action:SchedulePage.submit-schedule", Concepts: ["self-scheduling"])]);

        var done = await _rig.Service().Handle(new SetPagesRequest(Set: [page]), SetPagesRig.Token);

        Assert.True(done.IsSuccess);
        var edit = Assert.Single(_rig.Edits);
        Assert.Equal(PagePinId.From("schedule-week"), edit.Id);
        Assert.Null(edit.Repo);
        Assert.Equal("Schedule week", edit.Entry?.Name);
        var action = Assert.Single(edit.Entry?.Actions ?? []);
        Assert.Equal(PageActionPinId.From("submit-a-schedule"), action.Id);
        Assert.Equal(["self-scheduling"], action.Concepts);
        Assert.True(done.Value.Written);
        Assert.False(done.Value.Approved);
        Assert.Equal([new PageWritten(PagePinId.From("schedule-week"), "Schedule week", Repo: null, "updated")], done.Value.Pages);
    }
}
