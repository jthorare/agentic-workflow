using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>
/// #3734 review: during a views rebuild the status read that carries a streamed event's sequence waits its 2 s and gets
/// SND-API-011. An event that lands during that wait must not ride on the retry: the retry goes without X-Sindri-After
/// and shows what Sindri says (rebuilding), or every refresh ends "behind" for as long as events keep coming.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class SindriApiClientViewsBehindTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_event_during_a_views_behind_status_read_does_not_ride_on_the_retry()
    {
        var server = new FakeSindriApi { Lag = ViewsLag.Behind };
        server.Push(A.Event(7));
        var handler = new FakeSindriHandler(server);
        var client = new SindriApiClient(new HttpClient(handler), new InMemoryHumanToken("tok-1"), TimeProvider.System);
        var events = client.Events(_api, 0, Token).GetAsyncEnumerator(Token);
        await using (events.ConfigureAwait(false))
        {
            Assert.True(await events.MoveNextAsync());

            // The first status read carries 7 and is held, as the server holds it for its 2 s; event 9 lands meanwhile.
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            server.StatusGate = gate;
            var read = client.ReadStatus(_api, Token);
            server.Push(A.Event(9));
            Assert.True(await events.MoveNextAsync());
            gate.SetResult();
            _ = await read;
            server.Drop();
        }

        Assert.Equal(
            ["7", null],
            handler.Requests.Where(r => r.Path == "/sindri/status").Select(r => r.After),
            StringComparer.Ordinal);
    }
}
