using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.StartFromIssue;

namespace Aw.Sindri.Tests.LinearHubPh4;

/// <summary>The owner-only start command (Linear hub spec 2026-11-04 §6.1, TP-5): an item linked to the issue by the owner, then workflow.scoping on it; a second start is SND-LINEAR-007.</summary>
public sealed class StartFromIssueTests
{
    private readonly Ph4Rig _rig = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // red: step 2 passes the ports to the constructor (facts, items view, engine start); the stub takes none yet
    private StartFromIssueService Service()
    {
        ArgumentNullException.ThrowIfNull(_rig.Facts);
        return new StartFromIssueService();
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task Start_makes_an_item_titled_from_the_issue_linked_to_its_record_by_the_owner()
    {
        await _rig.Hold();
        using var entered = _rig.Owner();

        var started = await Service().Handle(new StartFromIssueRequest(Ph4Rig.Key), Token);

        Assert.True(started.IsSuccess, started.IsError ? started.Error.ToString() : null);
        var created = _rig.H.Events.Payload<ItemCreated>(Assert.Single(_rig.Events(EventTypes.ItemCreated))).Value;
        Assert.Equal((_rig.Record, Ph4Rig.Alias, CallerClass.Human, "Title of ENG-12"), (created.Link!.Value, created.LinkAlias!.Value, created.By!.Value, created.Title));
        Assert.Equal(started.Value.Item, created.Item);
        Assert.Empty(_rig.Events(EventTypes.ItemLinked));
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task Start_starts_workflow_scoping_on_the_new_item()
    {
        await _rig.Hold();
        using var entered = _rig.Owner();

        var started = await Service().Handle(new StartFromIssueRequest(Ph4Rig.Key), Token);

        Assert.True(started.IsSuccess, started.IsError ? started.Error.ToString() : null);
        Assert.Contains("/scoping/", started.Value.Instance, StringComparison.Ordinal);
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task A_second_start_on_a_held_issue_is_SND_LINEAR_007_with_the_specs_text()
    {
        await _rig.Hold();
        using var entered = _rig.Owner();
        var first = await Service().Handle(new StartFromIssueRequest(Ph4Rig.Key), Token);

        var second = await Service().Handle(new StartFromIssueRequest(Ph4Rig.Key), Token);

        Assert.True(second.IsError);
        Assert.Equal(("SND-LINEAR-007", ErrorKind.Conflict), (second.Error.Code, second.Error.Kind));
        Assert.Contains($"ENG-12 is linked to {first.Value.Item.Value} (scoping, running). Open it, or unlink it first.", second.Error.Message, StringComparison.Ordinal);
        _ = Assert.Single(_rig.Events(EventTypes.ItemCreated));
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task A_sessions_start_is_refused_SND_AUTH_002_and_makes_nothing()
    {
        await _rig.Hold();
        using var entered = _rig.Session();

        var started = await Service().Handle(new StartFromIssueRequest(Ph4Rig.Key), Token);

        Assert.True(started.IsError);
        Assert.Equal(SindriErrors.AuthRefused.Code, started.Error.Code);
        Assert.Empty(_rig.Events(EventTypes.ItemCreated));
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task An_unknown_issue_is_not_started()
    {
        await _rig.Hold();
        using var entered = _rig.Owner();

        var started = await Service().Handle(new StartFromIssueRequest("ENG-99"), Token);

        Assert.True(started.IsError);
        Assert.Empty(_rig.Events(EventTypes.ItemCreated));
    }
}
