using Aw.Sindri.Host.Commands;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri status --follow' (API P5 unit H): the summary again each time it changes, off the event stream; exit 0 on Ctrl-C, 1 when the server stops.</summary>
public sealed class CliStatusFollowTests : IDisposable
{
    private readonly FollowRig _rig = new();

    public void Dispose() => _rig.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task Follow_reprints_when_status_changes()
    {
        var api = new FollowApi(FollowRig.Status(needsApproval: true), FollowRig.Status(needsApproval: false));
        var run = _rig.Start(api, json: false);
        await FollowRig.WaitUntil(() => api.Streams == 1);

        await api.Frame(2, "service.heartbeat");
        await _rig.Tick(FollowVerbs.Debounce, () => api.StatusReads == 2);
        await _rig.Interrupt();

        Assert.Equal(0, await FollowRig.Finished(run));
        var text = _rig.Out.ToString().ReplaceLineEndings("\n");
        Assert.Contains("the live profile file has a problem", text, StringComparison.Ordinal);
        Assert.Contains("(hash", text, StringComparison.Ordinal);
        Assert.Contains("\n\n  profile", text, StringComparison.Ordinal);   // a blank line between the two summaries
        Assert.Equal("?after=0", api.StreamQuery);
        Assert.Equal("2", api.LastAfterHeader);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Follow_ignores_pings_and_unchanged_reads()
    {
        var api = new FollowApi(FollowRig.Status(needsApproval: true));
        var run = _rig.Start(api, json: false);
        await FollowRig.WaitUntil(() => api.Streams == 1);

        await api.Raw(": ping\n\n");
        await api.Frame(2, "service.heartbeat");   // a read that changes nothing
        await _rig.Tick(FollowVerbs.Debounce, () => api.StatusReads == 2);
        await _rig.Interrupt();

        Assert.Equal(0, await FollowRig.Finished(run));
        Assert.Equal(1, _rig.Out.ToString().Split("  profile", StringSplitOptions.None).Length - 1);   // printed once
        Assert.Equal(2, api.StatusReads);   // the ping caused no read of its own
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Two_frames_in_one_debounce_make_one_read_that_reflects_the_later_one()
    {
        var api = new FollowApi(FollowRig.Status(needsApproval: true), FollowRig.Status(needsApproval: false));
        var run = _rig.Start(api, json: false);
        await FollowRig.WaitUntil(() => api.Streams == 1);

        await api.Raw(FollowApi.Text(2, "service.heartbeat") + FollowApi.Text(3, "service.heartbeat"));
        await FollowRig.WaitUntil(() => _rig.Clock.Timers > 0);   // the debounce window is open
        await Task.Delay(50, TestContext.Current.CancellationToken);   // why: lets the second frame, already in the pipe, be taken into the window before it closes
        await _rig.Tick(FollowVerbs.Debounce, () => api.StatusReads == 2);
        await _rig.Interrupt();

        Assert.Equal(0, await FollowRig.Finished(run));
        Assert.Equal(2, api.StatusReads);
        Assert.Equal("3", Assert.Single(api.Afters.OfType<string>()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Follow_exits_1_when_the_stream_ends()
    {
        var api = new FollowApi(FollowRig.Status(needsApproval: true));
        var run = _rig.Start(api, json: false);
        await FollowRig.WaitUntil(() => api.Streams == 1);

        await api.End();

        Assert.Equal(1, await FollowRig.Finished(run));
        Assert.Contains("not running", _rig.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Follow_falls_back_to_polling_on_429()
    {
        var api = new FollowApi(FollowRig.Status(needsApproval: true), FollowRig.Status(needsApproval: false)) { StreamsFull = true };
        var run = _rig.Start(api, json: false);
        await FollowRig.WaitUntil(() => api.Streams == 1);

        await _rig.Tick(FollowVerbs.Poll, () => api.StatusReads >= 2);
        await _rig.Interrupt();

        Assert.Equal(0, await FollowRig.Finished(run));
        Assert.Contains("(hash", _rig.Out.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, api.Streams);   // it didn't ask for another stream
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Follow_json_prints_one_body_per_line()
    {
        var first = FollowRig.Status(needsApproval: true);
        var second = FollowRig.Status(needsApproval: false);
        var api = new FollowApi(first, second);
        var run = _rig.Start(api, json: true);
        await FollowRig.WaitUntil(() => api.Streams == 1);

        await api.Frame(2, "service.heartbeat");
        await _rig.Tick(FollowVerbs.Debounce, () => api.StatusReads == 2);
        await FollowRig.WaitUntil(() => _rig.Out.ToString().Contains(second, StringComparison.Ordinal));
        await _rig.Interrupt();

        Assert.Equal(0, await FollowRig.Finished(run));
        Assert.Equal([first, second], _rig.Out.ToString().ReplaceLineEndings("\n").TrimEnd().Split('\n'));
    }
}
