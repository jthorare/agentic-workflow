using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.CreateItem;
using Aw.Sindri.Features.FileClosedIssueRequests;
using Aw.Sindri.Features.LinearIssues;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.LinearIssues;

namespace Aw.Sindri.Tests.LinearHubPh4;

/// <summary>The pull side (Linear hub spec 2026-11-04 §6.3, TP-7): a linked issue that became completed or canceled while its item is open files one request; a restart or a views rebuild files nothing twice; the run is never stopped without the owner's answer.</summary>
public sealed class FileClosedIssueRequestsTests
{
    private readonly Ph4Rig _rig = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // red: step 2 passes the issue facts, the items view and the request filer to the constructor; the stub takes none yet
    private static FileClosedIssueRequestsService Service(FakeLinearIssueFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        return new FileClosedIssueRequestsService();
    }

    private static FakeLinearIssueFacts Closed(string stateType) =>
        new FakeLinearIssueFacts()
            .WithSource(LinearIssueRows.Connected(LinearIssueRows.Viewer))
            .WithIssues(LinearIssueRows.Row(Ph4Rig.Key, LinearIssueRows.Viewer, stateType: stateType))
            .WithDetails(new LinearIssueDetailFacts(LinearIssueRows.Row(Ph4Rig.Key, LinearIssueRows.Viewer, stateType: stateType), "Body.", Removed: false, []));

    private async Task LinkedOpenItem()
    {
        await _rig.Hold();
        using var entered = _rig.Owner();
        _ = await new CreateItemService(_rig.H.Profiles, _rig.H.Held, _rig.H.Store, _rig.H.Events, _rig.H.Ids, _rig.H.Callers)
            .Handle(new CreateItemRequest("Sync drops rows", Link: _rig.Record), Token);
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task A_completed_issue_of_an_open_item_files_one_request_with_the_specs_text()
    {
        await LinkedOpenItem();
        using var entered = _rig.Service();

        var pass = await Service(Closed("completed")).Handle(new FileClosedIssueRequestsRequest(), Token);

        Assert.True(pass.IsSuccess, pass.IsError ? pass.Error.ToString() : null);
        Assert.Equal(1, pass.Value.Filed);
        var filed = _rig.H.Events.Payload<RequestFiled>(Assert.Single(_rig.Events(EventTypes.RequestFiled))).Value;
        Assert.Equal("ENG-12 was closed in Linear. Stop the run, or keep it?", filed.Text);
        _ = Assert.NotNull(filed.Item);
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task A_canceled_issue_files_one_too()
    {
        await LinkedOpenItem();
        using var entered = _rig.Service();

        var pass = await Service(Closed("canceled")).Handle(new FileClosedIssueRequestsRequest(), Token);

        Assert.True(pass.IsSuccess, pass.IsError ? pass.Error.ToString() : null);
        _ = Assert.Single(_rig.Events(EventTypes.RequestFiled));
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task An_issue_still_open_files_nothing()
    {
        await LinkedOpenItem();
        using var entered = _rig.Service();

        var pass = await Service(Closed("started")).Handle(new FileClosedIssueRequestsRequest(), Token);

        Assert.True(pass.IsSuccess, pass.IsError ? pass.Error.ToString() : null);
        Assert.Empty(_rig.Events(EventTypes.RequestFiled));
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task A_second_pass_after_a_restart_files_nothing_again()
    {
        await LinkedOpenItem();
        using var entered = _rig.Service();
        _ = await Service(Closed("completed")).Handle(new FileClosedIssueRequestsRequest(), Token);

        var again = await Service(Closed("completed")).Handle(new FileClosedIssueRequestsRequest(), Token);

        Assert.True(again.IsSuccess, again.IsError ? again.Error.ToString() : null);
        Assert.Equal(0, again.Value.Filed);
        var only = Assert.Single(_rig.Events(EventTypes.RequestFiled));
        Assert.StartsWith("request.filed:", only.IdempotencyKey, StringComparison.Ordinal);
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task The_request_leaves_the_run_running()
    {
        await LinkedOpenItem();
        using var entered = _rig.Service();

        _ = await Service(Closed("completed")).Handle(new FileClosedIssueRequestsRequest(), Token);

        Assert.Empty(_rig.Events(EventTypes.ItemStateChanged));
    }
}
