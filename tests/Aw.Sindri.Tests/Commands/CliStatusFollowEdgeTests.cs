using Aw.Sindri.Host.Commands;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri status --follow' where the stream or the server doesn't cooperate: not running, an error frame, views that stall, a server that goes away.</summary>
public sealed class CliStatusFollowEdgeTests : IDisposable
{
    private const string ErrorFrame = "event: error\ndata: {\"code\":\"SND-LEDGER-002\",\"detail\":\"locked\"}\n\n";
    private readonly FollowRig _rig = new();

    public void Dispose() => _rig.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task Follow_exits_1_without_opening_a_stream_when_sindri_is_not_running()
    {
        var api = new FollowApi(FollowRig.Status(needsApproval: true)) { Down = true };

        Assert.Equal(1, await FollowRig.Finished(_rig.Start(api, json: false)));

        Assert.Equal(0, api.Streams);
        Assert.Contains("not running", _rig.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Follow_exits_2_when_the_first_read_fails_for_another_reason()
    {
        var api = new FollowApi(FollowRig.Status(needsApproval: true)) { StatusFails = true };

        Assert.Equal(2, await FollowRig.Finished(_rig.Start(api, json: false)));

        Assert.Equal(0, api.Streams);
        Assert.Contains("SND-API-005", _rig.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Follow_falls_back_to_polling_on_an_error_frame()
    {
        var api = new FollowApi(FollowRig.Status(needsApproval: true), FollowRig.Status(needsApproval: false));
        var run = _rig.Start(api, json: false);
        await FollowRig.WaitUntil(() => api.Streams == 1);

        await api.Raw(ErrorFrame);
        await _rig.Tick(FollowVerbs.Poll, () => api.StatusReads >= 2);
        await _rig.Interrupt();

        Assert.Equal(0, await FollowRig.Finished(run));
        Assert.Contains("(hash", _rig.Out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Follow_falls_back_to_polling_on_an_error_frame_that_follows_an_event_within_the_debounce()
    {
        var api = new FollowApi(FollowRig.Status(needsApproval: true), FollowRig.Status(needsApproval: true), FollowRig.Status(needsApproval: false));
        var run = _rig.Start(api, json: false);
        await FollowRig.WaitUntil(() => api.Streams == 1);

        await api.Raw(FollowApi.Text(2, "service.heartbeat") + ErrorFrame);
        await _rig.Tick(FollowVerbs.Poll, () => api.StatusReads >= 3);
        await _rig.Interrupt();

        Assert.Equal(0, await FollowRig.Finished(run));
        Assert.Contains("(hash", _rig.Out.ToString(), StringComparison.Ordinal);
        Assert.Empty(api.Afters.OfType<string>());   // every read after the error frame is a plain one: it keeps polling, it doesn't wait on the stream
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Follow_exits_1_when_the_server_is_gone_after_an_event()
    {
        var api = new FollowApi(FollowRig.Status(needsApproval: true));
        var run = _rig.Start(api, json: false);
        await FollowRig.WaitUntil(() => api.Streams == 1);

        api.Down = true;
        await api.Frame(2, "service.heartbeat");
        await _rig.Tick(FollowVerbs.Debounce, () => run.IsCompleted);

        Assert.Equal(1, await FollowRig.Finished(run));
        Assert.Contains("not running", _rig.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Follow_prints_the_summary_when_the_views_cannot_catch_up_to_the_event()
    {
        var api = new FollowApi(FollowRig.Status(needsApproval: true), FollowRig.Status(needsApproval: false)) { ViewsBehind = true };
        var run = _rig.Start(api, json: false);
        await FollowRig.WaitUntil(() => api.Streams == 1);

        await api.Frame(2, "service.heartbeat");
        await _rig.Tick(FollowVerbs.Debounce, () => api.StatusReads == 2);
        await _rig.Interrupt();

        Assert.Equal(0, await FollowRig.Finished(run));
        Assert.Contains("(hash", _rig.Out.ToString(), StringComparison.Ordinal);
        Assert.Equal([null, "2", null], api.Afters);   // the read that asked to reflect 2 was refused, so it was repeated plain
    }
}
