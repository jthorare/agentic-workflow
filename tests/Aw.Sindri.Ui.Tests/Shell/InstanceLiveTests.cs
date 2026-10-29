using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>
/// The follower and 'workflow.instance-changed' (API §5): a frame is "this instance's view changed", never a payload to fold. Frames of one instance
/// collapse into one re-read per 250 ms window, a view that lags the frame's state is read again a bounded number of times, the stream is opened before
/// the views are re-read, and a frame (it has no id) never moves the resume cursor.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class InstanceLiveTests : IDisposable
{
    private const string Address = "ITM-1/project/1";

    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly InlineUiContext _ui = new();
    private readonly HealthViewModel _vm;
    private readonly List<InstanceChange?> _reads = [];
    private bool _caughtUp = true;
    private Task _run = Task.CompletedTask;

    public InstanceLiveTests()
    {
        _vm = new HealthViewModel(_records, _server, _clock, new FakeClipboard());
        _vm.FollowInstances((_, change, _) =>
        {
            lock (_reads)
            {
                _reads.Add(change);
            }

            _server.Trace.Enqueue(change is null ? "requery-all" : "requery");
            return Task.FromResult(_caughtUp);
        });
    }

    public void Dispose()
    {
        _stop.Cancel();
        Assert.True(_run.Wait(TimeSpan.FromSeconds(2), CancellationToken.None), "Run ended when cancelled");
        _stop.Dispose();
        _ui.Dispose();
    }

    private int Reads(Func<InstanceChange?, bool> which)
    {
        lock (_reads)
        {
            return _reads.Count(which);
        }
    }

    // The trace lines of these names, in order.
    private List<string> Marked(params string[] names) => [.. _server.Trace.Where(t => names.Contains(t, StringComparer.Ordinal))];

    private int FrameReads => Reads(c => c is not null);

    private async Task Running()
    {
        _records.Write(A.Record());
        _run = _vm.Run(_stop.Token);
        await Eventually(() => _vm.IsRunning && Marked("requery-all").Count > 0, "running with the instance stream open");
    }

    // Time only moves when the test moves it: advance in small steps until the condition holds. After each step the follower's continuations
    // get a run of yields (they usually finish within them), and a 1 ms real wait covers work that hopped to another thread.
    private async Task Eventually(Func<bool> condition, string what)
    {
        for (var i = 0; i < 300 && !condition(); i++)
        {
            _clock.Advance(TimeSpan.FromMilliseconds(50));
            for (var y = 0; y < 50 && !condition(); y++)
            {
                await Task.Yield();
            }

            if (!condition())
            {
                await Task.Delay(1, TestContext.Current.CancellationToken);
            }
        }

        Assert.True(condition(), what);
    }

    // Lets the follower's continuations run without moving time: the fake clock stays where the test put it.
    private static async Task Settle()
    {
        for (var i = 0; i < 200; i++)
        {
            await Task.Yield();
        }
    }

    [Fact]
    public async Task Frames_of_one_instance_in_a_window_are_one_re_read_with_the_newest_state()
    {
        await Running();

        _server.PushChange(new InstanceChange(Address, "intake", 10), new InstanceChange(Address, "build", 11), new InstanceChange(Address, "checks", 12));
        await Settle();
        await Eventually(() => FrameReads == 1, "one re-read");
        await Settle();
        _clock.Advance(HealthViewModel.RefreshEvery);

        Assert.Equal(1, FrameReads);
        lock (_reads)
        {
            Assert.Equal(new InstanceChange(Address, "checks", 12), _reads.Single(c => c is not null));
        }
    }

    [Fact]
    public async Task Frames_of_two_instances_are_a_re_read_each()
    {
        await Running();

        _server.PushChange(new InstanceChange(Address, "build", 10), new InstanceChange("ITM-2/project/1", "intake", 11));
        await Eventually(() => FrameReads == 2, "a re-read for each");

        lock (_reads)
        {
            Assert.Equal([Address, "ITM-2/project/1"], _reads.OfType<InstanceChange>().Select(c => c.Instance).Order(StringComparer.Ordinal).ToList());
        }
    }

    [Fact]
    public async Task A_view_that_lags_the_frame_is_read_again_a_few_times_and_then_left_alone()
    {
        _caughtUp = false;
        await Running();

        _server.PushChange(new InstanceChange(Address, "checks", 10));
        await Eventually(() => FrameReads == 1 + HealthViewModel.InstanceRetries, "the first read and its retries");
        for (var i = 0; i < 30; i++)
        {
            _clock.Advance(HealthViewModel.RetryEvery);   // far more than the backoff adds up to
            await Settle();
        }

        Assert.Equal(1 + HealthViewModel.InstanceRetries, FrameReads);
    }

    [Fact]
    public async Task A_retry_is_dropped_when_a_newer_frame_for_the_instance_is_waiting()
    {
        _caughtUp = false;
        await Running();

        _server.PushChange(new InstanceChange(Address, "build", 10));
        await Eventually(() => FrameReads == 1, "the first read");
        _server.PushChange(new InstanceChange(Address, "checks", 11));
        await Settle();   // the newer frame waits; the retry for the old state has not run yet
        await Eventually(() => FrameReads >= 2, "the newer frame's read");
        for (var i = 0; i < 30; i++)
        {
            _clock.Advance(HealthViewModel.RetryEvery);
            await Settle();
        }

        lock (_reads)
        {
            _ = Assert.Single(_reads, c => c?.State == "build");
            Assert.Equal(1 + HealthViewModel.InstanceRetries, _reads.Count(c => c?.State == "checks"));
        }
    }

    [Fact]
    public async Task A_view_that_catches_up_on_the_retry_is_not_read_again()
    {
        _caughtUp = false;
        await Running();

        _server.PushChange(new InstanceChange(Address, "checks", 10));
        await Eventually(() => FrameReads == 1, "the first read");
        _caughtUp = true;
        await Eventually(() => FrameReads == 2, "the retry");
        for (var i = 0; i < 10; i++)
        {
            _clock.Advance(HealthViewModel.RetryEvery);
            await Settle();
        }

        Assert.Equal(2, FrameReads);
    }

    [Fact]
    public async Task Every_connect_opens_the_stream_first_and_then_re_reads_what_is_shown()
    {
        await Running();
        Assert.Equal(["stream-open", "requery-all"], Marked("stream-open", "requery-all"));

        _server.Drop();
        await Eventually(() => Marked("requery-all").Count == 2, "the reconnect's re-read");

        var lines = Marked("stream-open", "requery-all");
        Assert.Equal("stream-open", lines[^2]);
        Assert.Equal("requery-all", lines[^1]);
        Assert.Equal(Marked("stream-open").Count, Marked("requery-all").Count);
    }

    [Fact]
    public async Task A_frame_never_moves_the_resume_cursor()
    {
        await Running();
        _server.Push(A.Event(5));
        await Eventually(() => _vm.LastEvent.EndsWith("#5", StringComparison.Ordinal), "event 5");

        _server.PushChange(new InstanceChange(Address, "build", 4211));
        await Eventually(() => FrameReads == 1, "the frame");
        var streams = _server.StreamCursors.Count;
        _server.Drop();
        await Eventually(() => _server.StreamCursors.Count > streams, "the raw stream opened again");

        Assert.Equal(5, _server.StreamCursors[^1]);
    }

    [Fact]
    public async Task The_stream_asks_for_every_instance_by_type_alone_and_never_widens_a_glob()
    {
        await Running();

        Assert.Equal([null], _server.ChangeQueries);
    }

    [Fact]
    public async Task A_stream_that_ends_at_once_is_tried_again_without_taking_the_follow_down()
    {
        _server.StreamsEndAtOnce = true;
        _records.Write(A.Record());
        _run = _vm.Run(_stop.Token);

        await Eventually(() => _vm.IsRunning, "running while the streams end at once");
    }
}
