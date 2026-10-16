using System.Net;
using System.Text.Json;
using Aw.Sindri.Tests.Callers;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ListOutbox;

/// <summary>GET /sindri/outbox?state= on the real host (engine spec §6.3; engine P6 unit (c)): a query any caller may read; an unknown state is a 400.</summary>
public sealed class OutboxRouteTests : IAsyncLifetime
{
    private readonly CallerHost _host = new();

    public ValueTask InitializeAsync() => _host.InitializeAsync();

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact, Trait("Lane", "integration")]
    public async Task Any_caller_lists_the_outbox_by_state()
    {
        var all = await _host.Http.Ask(Get("/sindri/outbox", Human(_host.Token.Value)));
        var pending = await _host.Http.Ask(Get("/sindri/outbox?state=pending", (Host.SindriHeaders.Caller, "session")));
        var unknown = await _host.Http.Ask(Get("/sindri/outbox?state=sent", Human(_host.Token.Value)));

        Assert.True(all.Status == HttpStatusCode.OK, all.Body);
        Assert.True(pending.Status == HttpStatusCode.OK, pending.Body);
        Assert.Empty(JsonDocument.Parse(pending.Body).RootElement.GetProperty("commands").EnumerateArray());
        Assert.Equal(HttpStatusCode.BadRequest, unknown.Status);
    }
}
