using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>
/// #3734: an event that came from outside the app (a CLI write) is a write the views may not have caught up with yet.
/// The first view read after it waits for its sequence (X-Sindri-After), like the read after the app's own command.
/// The fake serves no todo route, so the request is seen (its header recorded) and answered 404; the header is what is asserted.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class SindriApiClientEventAfterTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_view_read_after_a_streamed_event_waits_for_its_sequence_once()
    {
        var server = new FakeSindriApi();
        server.Push(A.Event(7));
        var handler = new FakeSindriHandler(server);
        var client = new SindriApiClient(new HttpClient(handler), new InMemoryHumanToken("tok-1"), TimeProvider.System);

        await foreach (var _ in client.Events(_api, 0, Token))
        {
            server.Drop();
        }

        _ = await client.Todos(_api, "all", Token);
        _ = await client.Todos(_api, "all", Token);

        Assert.Equal(
            ["7", null],
            handler.Requests.Where(r => r.Path == "/sindri/todos").Select(r => r.After),
            StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_newer_event_replaces_the_pending_sequence_and_an_older_one_never_lowers_it()
    {
        var server = new FakeSindriApi();
        server.Push(A.Event(9), A.Event(8));
        var handler = new FakeSindriHandler(server);
        var client = new SindriApiClient(new HttpClient(handler), new InMemoryHumanToken("tok-1"), TimeProvider.System);

        var seen = 0;
        await foreach (var _ in client.Events(_api, 0, Token))
        {
            if (++seen == 2)
            {
                server.Drop();
            }
        }

        _ = await client.Todos(_api, "all", Token);

        Assert.Equal("9", Assert.Single(handler.Requests, r => r.Path == "/sindri/todos").After);
    }
}
