using System.Net;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Testing.Launch;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.Launches;

/// <summary>
/// Issue 1004's frozen check: taking over an instance whose agent Step has a live launch opens that launch's tmux session in the viewer, and the takeover's response and
/// 'GET /sindri/launches/{id}' carry the viewer record. With no usable viewer the takeover still succeeds and the viewer outcome is 'failed' with code 'no-viewer'.
/// Over the HTTP host with a fake instance runtime and a fake viewer; nothing but the behavior and the JSON field names is asserted.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class TakeoverViewerCheckTests
{
    private static readonly LaunchId _launch = LaunchId.From("LCH-" + new string('1', 32));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_takeover_opens_the_live_session_in_the_viewer_and_reports_it()
    {
        var viewer = new FakeTerminalViewer("warp", "Warp");
        var runtime = new TakeoverRuntime();
        await using var host = await Start(viewer, runtime);
        var id = await SeedLiveLaunch(host, runtime);

        var taken = await Takeover(host, id);

        Assert.Equal(HttpStatusCode.OK, taken.Status);
        var opened = Assert.Single(viewer.Requests);
        Assert.Equal("snd-" + _launch.Value, opened.Host.Name);
        var root = JsonDocument.Parse(taken.Body).RootElement;
        Assert.Equal("taken over", root.GetProperty("result").GetProperty("happened").GetString());
        Assert.Equal("opened", root.GetProperty("viewer").GetProperty("outcome").GetString());
        Assert.Equal("warp", root.GetProperty("viewer").GetProperty("kind").GetString());
        Assert.Equal(["tmux", "-L", "sindri-launch", "attach", "-t", "=snd-" + _launch.Value], Strings(root.GetProperty("attach")));

        await ViewScenarios.CatchUp(host, Token);
        var read = await host.Http.Ask(Get($"/sindri/launches/{_launch.Value}", ItemScenarios.Human(host)));
        var launched = JsonDocument.Parse(read.Body).RootElement.GetProperty("viewer");
        Assert.Equal("opened", launched.GetProperty("outcome").GetString());
        Assert.Equal("warp", launched.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task With_no_usable_viewer_the_takeover_succeeds_and_the_outcome_is_failed_no_viewer()
    {
        var viewer = new FakeTerminalViewer("print", "Print");
        viewer.Script(ViewerOutcome.Skipped);
        var runtime = new TakeoverRuntime();
        await using var host = await Start(viewer, runtime);
        var id = await SeedLiveLaunch(host, runtime);

        var taken = await Takeover(host, id);

        Assert.Equal(HttpStatusCode.OK, taken.Status);
        var root = JsonDocument.Parse(taken.Body).RootElement;
        Assert.Equal("taken over", root.GetProperty("result").GetProperty("happened").GetString());
        var shown = root.GetProperty("viewer");
        Assert.Equal("failed", shown.GetProperty("outcome").GetString());
        Assert.Equal("no-viewer", shown.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(shown.GetProperty("reason").GetString()));
        Assert.Equal(["tmux", "-L", "sindri-launch", "attach", "-t", "=snd-" + _launch.Value], Strings(root.GetProperty("attach")));
    }

    private static string[] Strings(JsonElement array) => [.. array.EnumerateArray().Select(static e => e.GetString()!)];

    private static Task<GoldenHost> Start(ITerminalViewer viewer, TakeoverRuntime runtime) =>
        GoldenHost.Start(temporary: true, Token, ItemScenarios.ProfileYaml, extra: services =>
        {
            _ = services.Replace(ServiceDescriptor.Singleton(viewer));
            _ = services.Replace(ServiceDescriptor.Singleton<IInstanceRuntime>(runtime));
            _ = services.Replace(ServiceDescriptor.Singleton<IInstanceReader>(static sp => sp.GetRequiredService<IInstanceRuntime>()));
            _ = services.Replace(ServiceDescriptor.Singleton<IInstanceDirectory>(static sp => (IInstanceDirectory)sp.GetRequiredService<IInstanceRuntime>()));
        });

    /// <summary>An item, and a live engine launch of its instance's agent Step (as the engine's launch records it: unattended, the viewer skipped).</summary>
    private static async Task<string> SeedLiveLaunch(GoldenHost host, TakeoverRuntime runtime)
    {
        await ViewScenarios.Approve(host, Token);
        var item = await ItemScenarios.Create(host, "Try the takeover", "demo");
        await ViewScenarios.CatchUp(host, Token);
        var address = InstanceAddress.From(item + "/step/1");
        runtime.Address = address;
        await LaunchScenarios.Started(host, _launch, item, ViewerOutcomeKind.Skipped, Token,
            viewer: new Aw.Sindri.Core.Events.LaunchViewer("none", Label: null, ViewerOutcomeKind.Skipped, Code: null, Detail: null),
            step: new LaunchStep(address, "build", 1));
        await ViewScenarios.CatchUp(host, Token);
        return WorkflowInstanceId.Of(address).Value;
    }

    private static Task<Callers.Answer> Takeover(GoldenHost host, string id) =>
        host.Http.Ask(Post($"/sindri/workflow/instances/{id}/commands", """{"instance":"-","kind":"takeover"}""", ItemScenarios.Human(host)));

    /// <summary>The instance runtime as far as a takeover reads it: any instance id resolves to the one address, and a takeover takes it over.</summary>
    private sealed class TakeoverRuntime : IInstanceDirectory, IInstanceRuntime
    {
        private static readonly RecipeVersion _recipe = new(RecipeId.From("workflow.step"), 1, RecipeHash.From("sha256:" + new string('a', 64)));
        public InstanceAddress? Address { get; set; }

        public ValueTask<Result<AttemptState?>> Latest(ItemKey item, RecipeId recipe, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<InstanceAddress>> Resolve(string reference, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<InstanceAddress>>(Address is { } known && (WorkflowInstanceId.Of(known).Value == reference || known.Value == reference)
                ? known
                : WorkflowErrors.InstanceNotFound.With("no instance " + reference));

        public ValueTask<Result<InstanceView>> Start(InstanceStart start, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<InstanceView>> Get(InstanceAddress address, CancellationToken cancellationToken) => ValueTask.FromResult<Result<InstanceView>>(View(address, "build"));

        public ValueTask<Result<CommandResult>> Send(InstanceAddress address, CommandNonce nonce, Trigger command, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<CommandResult>>(new CommandResult(View(address, "taken-over"), "taken over"));

        public ValueTask<Result<CommandResult>> OpenRequest(InstanceAddress address, LaunchId launch, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<Unit>> Signal(InstanceAddress address, WaitSatisfied delivered, CancellationToken cancellationToken) => throw new NotSupportedException();

        private static InstanceView View(InstanceAddress address, string state) =>
            new(address, _recipe, state, Detail: null, DateTimeOffset.FromUnixTimeMilliseconds(1_000), string.Empty, [], Question: null, Task: null, Run: null, Children: [], Outcome: null)
            { Id = WorkflowInstanceId.Of(address), Version = 1 };
    }
}
