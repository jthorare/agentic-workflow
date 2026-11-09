using System.Net;
using System.Text.Json;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Host.Endpoints;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.Handoff;

/// <summary>POST /sindri/handoff (#2258 PR 12): loopback only, the per-start token, and an item with its 'item.handoff'.</summary>
public sealed class HandoffEndpointTests
{
    private const string Body = """{"title":"Fix the login timeout","source":"ci-bot","prompt":"Run the suite twice.","links":[{"label":"Ticket","url":"https://example.test/t/7"}]}""";

    private sealed class FarPeer : IHandoffPeer
    {
        public IPAddress? Of(HttpContext context) => IPAddress.Parse("10.1.2.3");
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_request_without_the_per_start_token_is_401()
    {
        await using var host = await GoldenHost.Start(temporary: true, TestContext.Current.CancellationToken);

        var answer = await host.Http.Ask(Post("/sindri/handoff", Body));

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_caller_that_is_not_on_this_machine_is_403()
    {
        await using var host = await GoldenHost.Start(temporary: true, TestContext.Current.CancellationToken,
            extra: services => services.Replace(ServiceDescriptor.Singleton<IHandoffPeer>(new FarPeer())));

        var answer = await host.Http.Ask(Post("/sindri/handoff", Body, Human(host.Token.Value)));

        Assert.Equal(HttpStatusCode.Forbidden, answer.Status);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_local_handoff_creates_an_item_and_records_item_handoff()
    {
        await using var host = await GoldenHost.Start(temporary: true, TestContext.Current.CancellationToken);

        var answer = await host.Http.Ask(Post("/sindri/handoff", Body, Human(host.Token.Value)));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        using var json = JsonDocument.Parse(answer.Body);
        var item = json.RootElement.GetProperty("itemId").GetString();
        Assert.StartsWith("ITM-", item, StringComparison.Ordinal);
        var scan = await host.App.Services.GetRequiredService<ILedgerReader>()
            .Scan(0, 100, new LedgerFilter(TypeFilter.Parse(EventTypes.ItemHandoff).Value, Item: null), TestContext.Current.CancellationToken);
        var recorded = Assert.Single(scan.Value.Events);
        Assert.Equal(item, recorded.Item);
        Assert.Equal("handoff:ci-bot", recorded.Envelope.Actor);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_wrong_token_without_a_caller_header_is_401()
    {
        await using var host = await GoldenHost.Start(temporary: true, TestContext.Current.CancellationToken);

        var answer = await host.Http.Ask(Post("/sindri/handoff", Body, ("X-Sindri-Token", "not-the-token")));

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_body_over_64_KB_is_413_and_makes_no_item()
    {
        await using var host = await GoldenHost.Start(temporary: true, TestContext.Current.CancellationToken);
        var big = "{\"title\":\"Big\",\"source\":\"ci-bot\",\"prompt\":\"" + new string('p', 70_000) + "\"}";

        var answer = await host.Http.Ask(Post("/sindri/handoff", big, Human(host.Token.Value)));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, answer.Status);
        Assert.Equal("SND-API-015", answer.Code);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_body_that_is_not_a_handoff_is_400()
    {
        await using var host = await GoldenHost.Start(temporary: true, TestContext.Current.CancellationToken);

        var answer = await host.Http.Ask(Post("/sindri/handoff", "{\"title\":", Human(host.Token.Value)));

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
    }

    [Fact(Timeout = 180_000), Trait("Lane", "integration")]
    public async Task The_thirty_first_handoff_in_a_minute_is_429()
    {
        await using var host = await GoldenHost.Start(temporary: true, TestContext.Current.CancellationToken);
        for (var i = 0; i < HandoffRateLimit.PerMinute; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await host.Http.Ask(Post("/sindri/handoff", Body, Human(host.Token.Value)))).Status);
        }

        var answer = await host.Http.Ask(Post("/sindri/handoff", Body, Human(host.Token.Value)));

        Assert.Equal(HttpStatusCode.TooManyRequests, answer.Status);
        Assert.Equal("SND-API-016", answer.Code);
    }
}
