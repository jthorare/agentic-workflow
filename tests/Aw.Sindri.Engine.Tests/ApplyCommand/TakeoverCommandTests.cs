using System.Net;
using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.CompleteStep;
using Aw.Sindri.Tests.Server;
using Aw.Sindri.Tests.Workflows;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.ApplyCommand;

/// <summary>
/// 'workflow takeover' and 'handback' through the API's command chain, the engine's 'open-request' (UI spec E-6) and a
/// start taken over (E-19), against the pinned dev server (engine spec §3.8, §5.2, §5.5, §6.2; verification plan
/// journeys 5–7): a session may take over (class pause) and hand back (class desk); a handback of an
/// instance that isn't taken over is SND-WF-003 and records nothing; an open-request sent twice for one launch applies
/// once, with one ledger row; a start while a launch is requested starts taken over, the launch as its causation.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class TakeoverCommandTests(DevServer server) : IClassFixture<DevServer>, IDisposable
{
    private const string Launch = "LCH-0123456789abcdef0123456789abcdef";

    /// <summary>The first state of workflow.step is a desk Step; a desk wait is at least the 2d desk floor (W20).</summary>
    private static readonly TimeSpan _deskWait = TimeSpan.FromDays(2);

    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Session_may_take_over_and_hand_back()
    {
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken);
        var address = await Started(served, "T-60");
        var route = "/sindri/workflow/instances/" + Id(address) + "/commands";

        var (taken, takenBody) = await Post(served, route, Command(address, "takeover"), static r => r.AsSession());
        var (refused, refusedBody) = await Post(served, route, Command(address, "handback"), static r => r.AsSession());

        Assert.True(taken == HttpStatusCode.OK, takenBody);
        Assert.True(refused == HttpStatusCode.OK, refusedBody);
        Assert.NotNull((await served.Until(address, static v => v.Task is not null)).Task);
    }

    [Fact]
    public async Task Handback_when_not_taken_over_is_SND_WF_003_and_records_nothing()
    {
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken);
        var address = await Started(served, "T-61");
        var events = await served.Events(address);

        var handback = await served.Cli("handback", address);

        Assert.Equal(2, handback.Exit);
        Assert.StartsWith("SND-WF-003 ", handback.Stderr, StringComparison.Ordinal);
        Assert.Equal(events, await served.Events(address));
    }

    [Fact]
    public async Task Open_request_twice_with_the_same_launch_applies_once_with_one_ledger_row()
    {
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken);
        var address = await Started(served, "T-62");
        var runtime = served.Sindri.GetRequiredService<IInstanceRuntime>();
        var before = await served.Events(address);

        var first = await runtime.OpenRequest(InstanceAddress.From(address), LaunchId.From(Launch), Token);
        var afterFirst = await served.Events(address);
        var second = await runtime.OpenRequest(InstanceAddress.From(address), LaunchId.From(Launch), Token);

        Assert.False(first.IsError, first.IsError ? first.Error.Message : null);
        Assert.False(second.IsError, second.IsError ? second.Error.Message : null);
        Assert.Equal(("taken-over", "taken-over"), (first.Value.Instance.State, second.Value.Instance.State));
        Assert.Equal([.. before, "workflow.transition"], afterFirst);
        Assert.Equal(afterFirst, await served.Events(address));
    }

    [Fact]
    public async Task Start_with_a_requested_launch_starts_taken_over_with_the_launch_as_causation()
    {
        var launches = new InMemoryLaunchRequests();
        launches.Request(ItemKey.From("T-63"), LaunchId.From(Launch));
        await using var served = await Served.Start(_harness.Home, server.Environment, TestContext.Current.CancellationToken, s => s.AddSingleton<ILaunchRequests>(launches));

        var started = await served.Cli("start", "workflow.step", "--item", "T-63", "--fact", "fact:spec=" + _harness.File("spec.md", "the spec"));
        var view = await served.Until("T-63/step/1", static v => v.State == "taken-over");
        var read = await served.Sindri.GetRequiredService<ILedgerStore>().ReadAfter(0, 1000, Token);

        Assert.True(started.Exit == 0, started.Stderr);
        Assert.Null(view.Task);
        Assert.Equal(_deskWait, view.Paused);
        var row = Assert.Single(read.Value, static e => e.Envelope.Type == "workflow.started" && e.Envelope.Payload.Contains("\"T-63/step/1\"", StringComparison.Ordinal));
        Assert.Contains($"\"causation\":\"{Launch}\"", row.Envelope.Payload, StringComparison.Ordinal);

        var back = await served.Cli("handback", "T-63/step/1");
        var open = await served.Until("T-63/step/1", static v => v.Task is not null);
        Assert.True(back.Exit == 0, back.Stderr);
        var left = Assert.IsType<DateTimeOffset>(open.Task!.TimeoutAt) - RealTime.NowOffset;
        Assert.InRange(left, _deskWait - TimeSpan.FromMinutes(5), _deskWait);
    }

    private async Task<string> Started(Served served, string item)
    {
        var started = await served.Cli("start", "workflow.step", "--item", item, "--fact", "fact:spec=" + _harness.File("spec.md", "the spec"));
        Assert.True(started.Exit == 0, started.Stderr);
        var address = item + "/step/1";
        _ = await served.Until(address, static v => v.Task is not null);
        return address;
    }

    private static string Command(string address, string kind) => $$"""{ "instance": "{{address}}", "kind": "{{kind}}" }""";

    private static string Id(string address) => WorkflowInstanceId.Of(InstanceAddress.From(address)).Value;

    private static async Task<(HttpStatusCode Status, string Body)> Post(Served served, string route, string body, Func<HttpRequestMessage, HttpRequestMessage> caller)
    {
        using var request = caller(new HttpRequestMessage(HttpMethod.Post, route) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        using var answer = await served.Http.SendAsync(request, Token);
        return (answer.StatusCode, await answer.Content.ReadAsStringAsync(Token));
    }
}
