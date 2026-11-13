using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Tests.ApiGoldens;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ActivateWorkflow;

/// <summary>A session's POST to the activate and pause routes is refused with SND-AUTH-002 (403), the answer 'sindri wf activate' and 'sindri wf pause' print from a session; the human's call is not refused for that reason.</summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
[Lane(TestLane.Integration)]
public sealed class SessionRefusalOnTheRealHostTests
{
    [Fact(Timeout = 60_000)]
    public async Task A_session_is_refused_on_both_routes_and_the_human_is_not()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await GoldenHost.Start(temporary: true, ct);
        using var asSession = AsSession();

        var activate = await host.Http.Ask(Post("/sindri/workflows/nope/activate", json: null, Human(host.Token.Value)));
        var pause = await host.Http.Ask(Post("/sindri/workflows/nope/pause", json: null, Human(host.Token.Value)));
        using var asHuman = AsHuman();
        var owner = await host.Http.Ask(Post("/sindri/workflows/nope/activate", json: null, Human(host.Token.Value)));

        Assert.Equal((HttpStatusCode.Forbidden, "SND-AUTH-002"), (activate.Status, activate.Code));
        Assert.Equal((HttpStatusCode.Forbidden, "SND-AUTH-002"), (pause.Status, pause.Code));
        Assert.Equal((HttpStatusCode.NotFound, "SND-GRAPH-008"), (owner.Status, owner.Code));
    }
}
