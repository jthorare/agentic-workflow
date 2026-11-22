using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Features.OutboxRecord;
using Aw.Meta.Features.StopCheck;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Delegate;

public sealed class DelegationObligationTests
{
    private readonly DelegateWorld _d = new();

    private async Task<StopCheckResponse> StopCheck()
    {
        var service = new StopCheckService(_d.World.Session, _d.World.Sweep, _d.World.Guard, _d.World.Clock, _d.World.Beats);
        var result = await service.Handle(new StopCheckRequest(SessionId.From(MetaWorld.MetaSessionId), MetaWorld.Transcript, HookActive: false, MaxBlocks: 3), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    private async Task<OutboxRecordResponse> Send(string to, string message)
    {
        var service = new OutboxRecordService(_d.World.Session, _d.World.Messages, _d.World.Reader, _d.World.Clock);
        var result = await service.Handle(new OutboxRecordRequest(SessionId.From(MetaWorld.MetaSessionId), to, message, Id: null), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    private async Task DelegateToApi()
    {
        await _d.Ready();
        _d.World.Transcripts.Write(MetaWorld.Transcript, string.Empty);   // stop-check syncs the meta transcript first; an empty one holds no message
        _ = await _d.Delegate(DelegateWorld.Request());
        _d.World.Clock.Advance(TimeSpan.FromMinutes(1));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Stop_check_blocks_on_an_owed_delegation_and_names_the_target_and_the_text()
    {
        await DelegateToApi();

        var response = await StopCheck();

        Assert.True(response.Blocked);
        var owed = Assert.Single(response.Open);
        Assert.Equal((ObligationKind.Delegate, "#77"), (owed.Kind, owed.Key));
        Assert.Equal("delegation #77 not sent to t1-api: Owner wants a dark theme toggle", owed.Summary);
        Assert.StartsWith("SendMessage to t1-api with this text:\nACTION: #77 Owner wants a dark theme toggle\n", owed.NextCommand, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_send_to_the_lane_naming_the_issue_settles_it()
    {
        await DelegateToApi();

        var response = await Send("t1-api", "ACTION: #77 Owner wants a dark theme toggle\nhttps://x\nYours: take it.");

        Assert.Equal([(ObligationKind.Delegate, "#77")], response.Closed.Select(static o => (o.Kind, o.Key)));
        Assert.False((await StopCheck()).Blocked);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("t1-ui", "ACTION: #77 Owner wants a dark theme toggle")]
    [InlineData("t1-api", "ACTION: Owner wants a dark theme toggle")]
    [InlineData("t1-api", "see #770")]
    public async Task A_send_to_another_lane_or_without_the_issue_number_leaves_it_owed(string to, string message)
    {
        await DelegateToApi();

        var response = await Send(to, message);

        Assert.DoesNotContain(response.Closed, static o => o.Kind == ObligationKind.Delegate);
        Assert.Contains((await StopCheck()).Open, static o => o.Kind == ObligationKind.Delegate && o.Key == "#77");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_send_before_the_delegation_does_not_settle_it()
    {
        await _d.Ready();
        _ = await _d.World.Messages.RecordOutbound(A.Sent("t1-api", "heads up: #77 is coming", minute: 30), CancellationToken.None);

        _ = await _d.Delegate(DelegateWorld.Request());

        Assert.Contains(await _d.World.Open(), static o => o.Kind == ObligationKind.Delegate);
    }
}
