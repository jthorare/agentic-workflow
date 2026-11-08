using Aw.Sindri.Core.Memory;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.Memory;

/// <summary>GET /sindri/memories/{name}/search through the real host: MemoryFencing takes its JSON options from DI, so only a started host proves the wiring.</summary>
public sealed class MemorySearchFenceApiTests
{
    private const string FactText = "Deploys go out on Tuesdays";

    // as it sits inside a JSON string
    private const string Fenced = "<untrusted source=\\\"sindri:memory\\\">" + FactText + "</untrusted>";

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task A_session_reads_memory_search_hits_fenced_and_the_owner_reads_them_plain()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await GoldenHost.Start(temporary: true, ct,
            extra: services => { _ = services.RemoveAll<IMemoryEmbedder>(); _ = services.AddSingleton<IMemoryEmbedder>(new FakeMemoryEmbedder()); });
        var owner = Human(host.Token.Value);

        var created = await host.Http.Ask(Post("/sindri/memories", """{"name":"team-notes"}""", owner));
        var written = await host.Http.Ask(Post("/sindri/memories/team-notes/facts", $$"""{"text":"{{FactText}}"}""", owner));
        Assert.True(created.Status == System.Net.HttpStatusCode.OK && written.Status == System.Net.HttpStatusCode.OK, $"seed: {created.Status} {created.Body} / {written.Status} {written.Body}");

        const string Url = "/sindri/memories/team-notes/search?query=deploys";
        var session = await host.Http.Ask(Get(Url, [(SindriHeaders.Caller, "session"), (SindriHeaders.Repo, "any")]));
        var human = await host.Http.Ask(Get(Url, owner));

        Assert.True(session.Status == System.Net.HttpStatusCode.OK && human.Status == System.Net.HttpStatusCode.OK, $"{session.Status} {session.Body} / {human.Status} {human.Body}");
        Assert.True(session.Body.Contains(Fenced, StringComparison.Ordinal), $"session body is not fenced: {session.Body}");
        Assert.True(human.Body.Contains(FactText, StringComparison.Ordinal) && !human.Body.Contains("<untrusted", StringComparison.Ordinal), $"owner body is not plain: {human.Body}");
    }
}
