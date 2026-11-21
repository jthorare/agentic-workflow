using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>FakeSindriApi.StreamCursors is read while streams open on other threads (#3450): a read must never see the list mid-write.</summary>
[Lane(TestLane.Unit)]
public sealed class FakeStreamCursorsTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    [Fact]
    public async Task Reading_the_cursors_while_streams_open_on_other_threads_never_throws()
    {
        var server = new FakeSindriApi { StreamsEndAtOnce = true };
        const int opens = 20000;
        using var stop = new CancellationTokenSource();
        var reader = Task.Run(() =>
        {
            var last = 0;
            while (!stop.IsCancellationRequested)
            {
                last = server.StreamCursors.Count;
            }

            return last;
        }, CancellationToken.None);

        await Task.WhenAll(Enumerable.Range(0, 4).Select(worker => Task.Run(async () =>
        {
            for (var i = 0; i < opens / 4; i++)
            {
                await foreach (var e in server.Events(_api, i, CancellationToken.None))
                {
                    _ = e;
                }
            }
        }, CancellationToken.None)));
        await stop.CancelAsync();

        _ = await reader;   // a torn read throws here
        Assert.Equal(opens, server.StreamCursors.Count);
    }
}
