using System.Net;
using System.Text;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Tests.CompleteStep;
using Aw.Sindri.Tests.Server;
using Aw.Sindri.Tests.Workflows;

namespace Aw.Sindri.Tests.ApplyCommand;

/// <summary>
/// PB spec §13 (PB-E): the babysitter's merge and rework are recipe verbs of command, a desk row; the app's human is not refused by authorization (the engine then answers about the instance, not about the caller).
/// </summary>
[Lane(TestLane.Integration)]
public sealed class BabysitterVerbsRefusedTests(DevServer server) : IClassFixture<DevServer>, IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Theory(Timeout = 120_000)]
    [InlineData("merge")]
    [InlineData("rework")]
    public async Task The_apps_human_is_not_refused_by_authorization(string verb)
    {
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken);
        var address = await Started(served);

        var (status, problem) = await Post(served, address, verb, r => r.AsHuman(served.App, terminal: false));

        Assert.NotEqual(HttpStatusCode.Forbidden, status);
        Assert.False(problem.ValueKind == JsonValueKind.Object && problem.TryGetProperty("title", out var title) && string.Equals(title.GetString(), "SND-AUTH-002", StringComparison.Ordinal));
    }

    // An instance on an item in the repo "demo": a session working there can see it, so its refusal is the authorization's and not a 404.
    private async Task<string> Started(Served served)
    {
        var item = await HandbackShortFormTests.ItemIn(served, "demo");
        var started = await served.Cli("start", "workflow.step", "--item", item, "--fact", "fact:spec=" + _harness.File("spec.md", "the spec"));
        Assert.True(started.Exit == 0, started.Stderr);
        var address = item + "/step/1";
        _ = await served.Until(address, static v => v.Task is not null);
        return address;
    }

    private static async Task<(HttpStatusCode Status, JsonElement Problem)> Post(Served served, string address, string verb, Func<HttpRequestMessage, HttpRequestMessage> caller)
    {
        var route = "/sindri/workflow/instances/" + WorkflowInstanceId.Of(InstanceAddress.From(address)).Value + "/commands";
        var body = $$"""{ "instance": "{{address}}", "kind": "command", "verb": "{{verb}}" }""";
        using var request = caller(new HttpRequestMessage(HttpMethod.Post, route) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        using var answer = await served.Http.SendAsync(request, TestContext.Current.CancellationToken);
        var text = await answer.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return (answer.StatusCode, text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone());
    }
}
