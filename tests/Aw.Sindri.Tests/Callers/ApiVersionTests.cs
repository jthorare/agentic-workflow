using System.Net;
using Aw.Sindri.Host;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.Callers;

/// <summary>Every answer carries Sindri-Api-Version: 1, and GET /status reports it (API spec §9).</summary>
public sealed class ApiVersionTests(CallerHost host) : IClassFixture<CallerHost>
{
    [Fact, Trait("Lane", "integration")]
    public async Task Every_response_carries_Sindri_Api_Version_1()
    {
        var answers = new[]
        {
            await host.Http.Ask(Get("/sindri/status")),
            await host.Http.Ask(Get("/sindri/ping")),
            await host.Http.Ask(Get("/sindri/no-such-route")),
            await host.Http.Ask(Get("/sindri/status", (SindriHeaders.Caller, "admin"))),
            await host.Http.Ask(Get("/sindri/status", Human("stale"))),
            await host.Http.Ask(Post("/sindri/stop", json: null, ("Origin", "http://localhost"))),
            await host.Http.Ask(Post("/sindri/profile/approve", """{"hash":""")),
        };

        Assert.Equal(
            [HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden, HttpStatusCode.Gone],
            answers.Select(a => a.Status));
        Assert.All(answers, a => Assert.Equal("1", a.ApiVersion));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Status_reports_api_version_1()
    {
        var answer = await host.Http.Ask(Get("/sindri/status"));

        Assert.Equal(1, System.Text.Json.JsonDocument.Parse(answer.Body).RootElement.GetProperty("apiVersion").GetInt32());
    }
}
