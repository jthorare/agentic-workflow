using Aw.Context.Testing.Fixtures;
using Aw.Context.Testing.Map;
using Aw.Sindri.Host;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>GET /sindri/context/pins through the real host and engine: the rebuild state reads the publish status, which the engine refuses a session, so the read must not read it as the session.</summary>
public sealed class ContextPinsSessionApiTests : IDisposable
{
    private readonly FixtureRepo _repo = FixtureRepo.Build();

    public void Dispose() => _repo.Dispose();

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_session_reads_its_pins_and_the_rebuild_state_through_the_real_engine()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await ContextHosts.Approved(_repo, sessions: true, ct, name: FixtureMap.Repo);

        var session = await host.Http.Ask(Get("/sindri/context/pins", [(SindriHeaders.Caller, "session"), (SindriHeaders.Repo, FixtureMap.Repo)]));
        var human = await host.Http.Ask(Get("/sindri/context/pins", Human(host.Token.Value)));

        Assert.True(session.Status == System.Net.HttpStatusCode.OK && human.Status == System.Net.HttpStatusCode.OK, $"{session.Status} {session.Body} / {human.Status} {human.Body}");
        Assert.Contains("\"rebuild\"", session.Body, StringComparison.Ordinal);
    }
}
