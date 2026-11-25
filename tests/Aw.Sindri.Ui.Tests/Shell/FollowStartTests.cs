using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

// Where the event stream opens: a fresh connect and a new ledger start at the status's ledger head (#2778);
// a dropped stream on the same process and epoch resumes from the cursor of the events it received.
[Lane(TestLane.Unit)]
public sealed class FollowStartTests : IDisposable
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly HealthViewModel _vm;
    private readonly InlineUiContext _ui = new();
    private Task _run = Task.CompletedTask;

    public FollowStartTests()
    {
        _vm = new HealthViewModel(_records, _server, _clock, new FakeClipboard());
    }

    public void Dispose()
    {
        _stop.Cancel();
        Assert.True(_run.Wait(TimeSpan.FromSeconds(2), CancellationToken.None), "Run ended when cancelled");
        _stop.Dispose();
        _ui.Dispose();
    }

    private void Start() => _run = _vm.Run(_stop.Token);

    private static StatusView WithHead(StatusView status, long head) =>
        status with { Views = new AsOfView(Cursor: head, LedgerHead: head, ProjectedAt: A.Noon, Rebuilding: false, Stalled: false) };

    private static async Task Eventually(Func<bool> condition, string what)
    {
        for (var i = 0; i < 2000 && !condition(); i++)
        {
            if (i < 50)
            {
                await Task.Yield();
            }
            else
            {
                await Task.Delay(1, TestContext.Current.CancellationToken);
            }
        }

        Assert.True(condition(), what);
    }

    private async Task<long> ReconnectCursor(int before)
    {
        await Eventually(() => _server.StreamCursors.Count > before, "reconnect stream opened");
        return _server.StreamCursors[^1];
    }

    [Fact]
    public async Task A_first_connect_starts_the_stream_at_the_ledger_head()
    {
        _records.Write(A.Record());
        _server.Current = WithHead(A.Status(), 10_000);
        Start();

        await Eventually(() => _server.StreamCursors.Count >= 1, "first stream opened");
        Assert.Equal(9_999, _server.StreamCursors[0]);
    }

    [Fact]
    public async Task A_connect_to_a_new_epoch_starts_at_the_new_ledger_head()
    {
        _records.Write(A.Record(epoch: 7));
        _server.Current = WithHead(A.Status(epoch: 7), 20);   // event 30 lies past the head, so the first stream (opened at 19) delivers it
        _server.Push(A.Event(30));
        Start();
        await Eventually(() => _vm.LastEvent.EndsWith("#30", StringComparison.Ordinal), "event 30");

        var before = _server.StreamCursors.Count;
        _records.Write(A.Record(epoch: 1));
        _server.Pong = new(4242, 1);
        _server.Current = WithHead(A.Status(epoch: 1), 2_000);
        await Eventually(() =>
        {
            _clock.Advance(HealthViewModel.RetryEvery);
            return _vm.Lease == "pid 4242 · epoch 1";
        }, "reconnected to the new ledger");
        Assert.Equal(1_999, await ReconnectCursor(before));
    }

    [Fact]
    public async Task A_connect_to_another_process_at_the_same_epoch_starts_at_its_head()
    {
        _records.Write(A.Record(pid: 4242, epoch: 1));
        _server.Pong = new(4242, 1);
        _server.Current = WithHead(A.Status(epoch: 1), 20);   // as above
        _server.Push(A.Event(30));
        Start();
        await Eventually(() => _vm.LastEvent.EndsWith("#30", StringComparison.Ordinal), "event 30");

        var before = _server.StreamCursors.Count;
        _server.Pong = new(5151, 1);
        _server.Current = WithHead(A.Status(pid: 5151, epoch: 1), 300);
        _records.Write(A.Record(pid: 5151, epoch: 1));
        await Eventually(() =>
        {
            _clock.Advance(HealthViewModel.RetryEvery);
            return _vm.Lease == "pid 5151 · epoch 1";
        }, "followed the new process");
        Assert.Equal(299, await ReconnectCursor(before));
    }

    [Fact]
    public async Task A_dropped_stream_on_the_same_process_resumes_from_its_cursor_not_the_head()
    {
        _records.Write(A.Record());
        Start();
        await Eventually(() => _vm.Headline == "Running", "running");
        _server.Push(A.Event(30));
        await Eventually(() => _vm.LastEvent.EndsWith("#30", StringComparison.Ordinal), "event 30");

        _records.Remove();
        _server.GoDown();
        await Eventually(() => _vm.Headline == "Not running", "down");

        var before = _server.StreamCursors.Count;
        _server.ComeUp();
        _server.Current = WithHead(A.Status(), 500);
        _records.Write(A.Record());
        await Eventually(() =>
        {
            _clock.Advance(HealthViewModel.RetryEvery);
            return _server.StreamCursors.Count > before;
        }, "reconnected");
        Assert.Equal(30, _server.StreamCursors[^1]);
    }

    [Fact]
    public async Task A_status_without_views_still_starts_at_zero()
    {
        _records.Write(A.Record());
        _server.Current = A.Status() with { Views = null };
        Start();

        await Eventually(() => _server.StreamCursors.Count >= 1, "first stream opened");
        Assert.Equal(0, _server.StreamCursors[0]);
    }
}
