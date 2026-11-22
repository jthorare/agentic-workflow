using System.Globalization;
using System.Net;
using System.Text.Json;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Home;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.HomeRoutes;

/// <summary>GET /sindri/home and /sindri/home/rows/{id} over a real control plane (home spec §8): the first page of 60, an unknown row, an encoded id, and a session's ledger read leaving 'home.*' out.</summary>
public sealed class HomeEndpointTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Task<GoldenHost> Start(InMemoryHomeView view) =>
        GoldenHost.Start(temporary: true, Token, ItemScenarios.ProfileYaml, extra: services =>
        {
            _ = services.RemoveAll<IHomeViewReader>();
            _ = services.AddSingleton<IHomeViewReader>(view);
        });

    [Fact, Trait("Lane", "integration")]
    public async Task Get_home_reads_first_page_60()
    {
        var view = HomeFixture.View();
        for (var i = 1; i <= 70; i++)
        {
            view.Put(HomeFixture.Row($"gh:review:a/b#{i.ToString(CultureInfo.InvariantCulture)}", position: 100 + i, i % 2 == 0 ? HomeGroup.Moving : HomeGroup.NeedsYou));
        }

        await using var host = await Start(view);
        await ViewScenarios.Approve(host, Token);

        var answer = await host.Http.Ask(Get("/sindri/home", ItemScenarios.Human(host)));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var root = JsonDocument.Parse(answer.Body).RootElement;
        Assert.Equal(60, root.GetProperty("needsYou").GetArrayLength() + root.GetProperty("moving").GetArrayLength());
        Assert.Equal(JsonValueKind.String, root.GetProperty("cursor").ValueKind);
        Assert.Equal(41, root.GetProperty("counts").GetProperty("needsYou").GetInt32());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_unknown_row_is_404_with_SND_HOME_002()
    {
        await using var host = await Start(HomeFixture.View());
        await ViewScenarios.Approve(host, Token);

        var answer = await host.Http.Ask(Get($"/sindri/home/rows/{HomeRowId.From("gh:review:a/b#999").PathSegment()}", ItemScenarios.Human(host)));

        Assert.Equal((HttpStatusCode.NotFound, "SND-HOME-002"), (answer.Status, answer.Code));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_row_id_with_colons_slashes_and_a_hash_is_read_percent_encoded()
    {
        await using var host = await Start(HomeFixture.View());
        await ViewScenarios.Approve(host, Token);
        var segment = HomeRowId.From(HomeFixture.Review).PathSegment();

        var answer = await host.Http.Ask(Get($"/sindri/home/rows/{segment}", ItemScenarios.Human(host)));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(HomeFixture.Review, JsonDocument.Parse(answer.Body).RootElement.GetProperty("row").GetProperty("id").GetString());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_rank_explain_for_an_id_with_slashes_and_a_hash_is_read_percent_encoded()
    {
        await using var host = await Start(HomeFixture.View());
        await ViewScenarios.Approve(host, Token);
        var segment = HomeRowId.From(HomeFixture.Review).PathSegment();

        var answer = await host.Http.Ask(Get($"/sindri/home/rank/{segment}", ItemScenarios.Human(host)));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(HomeFixture.Review, JsonDocument.Parse(answer.Body).RootElement.GetProperty("id").GetString());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_unknown_encoded_row_names_the_decoded_id()
    {
        await using var host = await Start(HomeFixture.View());
        await ViewScenarios.Approve(host, Token);

        var answer = await host.Http.Ask(Get($"/sindri/home/rows/{HomeRowId.From("gh:review:a/b#999").PathSegment()}", ItemScenarios.Human(host)));

        Assert.Equal((HttpStatusCode.NotFound, "SND-HOME-002"), (answer.Status, answer.Code));
        Assert.Contains("gh:review:a/b#999", answer.Body, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Session_ledger_hides_home_events()
    {
        await using var host = await Start(HomeFixture.View());
        await ViewScenarios.Approve(host, Token);
        await ViewScenarios.Append(host, EventTypes.HomeRowRaised, new HomeRowRaised(HomeFixture.Rows()[0], []), "home.row.raised:fixture", Token);

        var asHuman = await host.Http.Ask(Get("/sindri/ledger?type=home.*", ItemScenarios.Human(host)));
        var asSession = await host.Http.Ask(Get("/sindri/ledger?type=home.*", ItemScenarios.Session("demo")));

        Assert.Equal(["home.row.raised"], Types(asHuman.Body));
        Assert.Empty(Types(asSession.Body));
    }

    private static string[] Types(string body) => [.. JsonDocument.Parse(body).RootElement.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("type").GetString()!)];
}
