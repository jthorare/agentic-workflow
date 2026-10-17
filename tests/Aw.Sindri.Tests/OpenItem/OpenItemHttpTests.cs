using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Tests.ApiGoldens;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.OpenItem;

/// <summary>POST /sindri/items/{itemId}/open over HTTP (UI spec E-1): the refusals that happen before the service runs.</summary>
[Lane(TestLane.Integration)]
public sealed class OpenItemHttpTests
{
    private const string Body = """{"mode":"open","itemVersion":1}""";

    [Fact(Timeout = 30_000)]
    public async Task Open_with_no_caller_header_is_403()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await GoldenHost.Start(temporary: true, ct, ItemScenarios.ProfileYaml);

        var answer = await host.Http.Ask(Post("/sindri/items/ITM-00000000000000000000000000000001/open", Body));

        Assert.Equal(HttpStatusCode.Forbidden, answer.Status);
    }

    [Fact(Timeout = 30_000)]
    public async Task Target_not_matching_AgentId_pattern_is_400()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await GoldenHost.Start(temporary: true, ct, ItemScenarios.ProfileYaml);

        var answer = await host.Http.Ask(Post("/sindri/items/ITM-00000000000000000000000000000001/open", """{"mode":"open","target":"Claude Code","itemVersion":1}""", Human(host.Token.Value)));

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
    }

    [Fact(Timeout = 30_000)]
    public async Task An_unknown_mode_is_400()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await GoldenHost.Start(temporary: true, ct, ItemScenarios.ProfileYaml);

        var answer = await host.Http.Ask(Post("/sindri/items/ITM-00000000000000000000000000000001/open", """{"mode":"teleport","itemVersion":1}""", Human(host.Token.Value)));

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
    }
}
