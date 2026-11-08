using System.Net;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Tests.CompleteStep;
using Aw.Sindri.Tests.Server;
using Aw.Sindri.Tests.Workflows;

namespace Aw.Sindri.Tests.ApplyCommand;

/// <summary>
/// The engine's write routes through API P1's command chain (engine spec rev 8 §6.2, §6.3; "Phase 4" signal 5): since owner
/// decision q93 an agent session may start, answer, command and cancel like a human, and any caller that may command an instance may
/// ask for its nonce.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class WorkflowRoutesTests(DevServer server) : IClassFixture<DevServer>, IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 120_000)]
    public async Task An_agent_session_may_ask_for_a_nonce_for_an_instance()
    {
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken);
        _ = await served.Cli("start", "workflow.step", "--item", "T-33", "--fact", "fact:spec=" + _harness.File("spec.md", "the spec"));
        _ = await served.Until("T-33/step/1", static v => v.Task is not null);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/sindri/workflow/instances/{Id("T-33/step/1")}/nonce").AsSession();
        using var answer = await served.Http.SendAsync(request, TestContext.Current.CancellationToken);
        var body = await answer.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.True(answer.StatusCode == HttpStatusCode.OK, body);
        Assert.Matches("^[0-9a-f]{32}$", JsonDocument.Parse(body).RootElement.GetProperty("nonce").GetString());
    }

    private static string Id(string address) => WorkflowInstanceId.Of(InstanceAddress.From(address)).Value;
}
