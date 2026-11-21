using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Features.ActivateWorkflow;
using Aw.Sindri.Features.PauseWorkflow;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.WorkflowWrites;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using static Aw.Sindri.Tests.WorkflowWrites.WorkflowWriteRig;
using HostWorkflowTools = Aw.Sindri.Host.Mcp.WorkflowTools;

namespace Aw.Sindri.Tests.ActivateWorkflow;

/// <summary>Activate and Pause are the owner's (workflows page spec §8): a session is refused with SND-AUTH-002 in the service and in the MCP tools, and nothing changes; the owner still activates and pauses.</summary>
[Lane(TestLane.Unit)]
public sealed class SessionRefusalTests : IDisposable
{
    private readonly WorkflowWriteRig _rig = new();

    public void Dispose() => _rig.Dispose();

    private static JsonNode Structured(CallToolResult result) => JsonNode.Parse(result.StructuredContent!.Value.GetRawText())!;   // why: every tool result carries structuredContent

    private IDisposable EnterSession()
    {
        var launch = LaunchId.From("LCH-" + new string('0', 31) + "1");
        return _rig.Harness.Callers.Enter(new CallerContext(CallerClass.Session, Terminal: false, "session:launch:" + launch.Value, _rig.Harness.Ids.New<CommandId>(), _rig.Harness.Ids.New<CorrelationId>(), Repo: null, launch));
    }

    private IDisposable EnterHuman() =>
        _rig.Harness.Callers.Enter(new CallerContext(CallerClass.Human, Terminal: true, "human:cli", _rig.Harness.Ids.New<CommandId>(), _rig.Harness.Ids.New<CorrelationId>(), Repo: null));

    private ActivateWorkflowService Activate() => new(_rig.Workflows, _rig.Checker, _rig.Log, _rig.Harness.Callers);

    private PauseWorkflowService Pause() => new(_rig.Workflows, _rig.Log, _rig.Harness.Callers);

    private ServiceProvider Services() => new ServiceCollection()
        .AddSingleton(AwJson.CreateOptions(SindriJsonContext.Default))
        .AddSingleton(_rig.Harness.Callers)
        .AddSingleton<IService<ActivateWorkflowRequest, ActivateWorkflowResponse>>(Activate())
        .AddSingleton<IService<PauseWorkflowRequest, PauseWorkflowResponse>>(Pause())
        .BuildServiceProvider();

    private static ValueTask<CallToolResult> Call(IServiceProvider services, string tool) =>
        HostWorkflowTools.Call(tool, services, new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["slug"] = JsonSerializer.SerializeToElement(Slug.Value) }, Token);

    [Fact]
    public async Task A_session_that_activates_is_refused_and_the_workflow_stays_off()
    {
        await _rig.Seed();
        using var caller = EnterSession();

        var result = await Activate().Handle(new ActivateWorkflowRequest(Slug, Revision: null, "session:s1"), Token);

        Assert.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
        Assert.Null((await _rig.Workflows.Get(Id, Token)).Value.ActiveRevision);
        Assert.Empty(_rig.Recorded(EventTypes.WorkflowActivated));
    }

    [Fact]
    public async Task A_session_that_pauses_is_refused_and_the_workflow_stays_on()
    {
        await _rig.Seed();
        _ = (await _rig.Workflows.Activate(Id, 1, Token)).Value;
        using var caller = EnterSession();

        var result = await Pause().Handle(new PauseWorkflowRequest(Slug, "session:s1"), Token);

        Assert.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
        Assert.Equal(1, (await _rig.Workflows.Get(Id, Token)).Value.ActiveRevision);
        Assert.Empty(_rig.Recorded(EventTypes.WorkflowPaused));
    }

    [Fact]
    public async Task The_owner_still_activates_and_pauses()
    {
        await _rig.Seed();
        using var caller = EnterHuman();

        var on = await Activate().Handle(new ActivateWorkflowRequest(Slug, Revision: null, "human:cli"), Token);
        var off = await Pause().Handle(new PauseWorkflowRequest(Slug, "human:cli"), Token);

        Assert.True(on.IsSuccess);
        Assert.True(off.IsSuccess);
        Assert.Null((await _rig.Workflows.Get(Id, Token)).Value.ActiveRevision);
    }

    [Fact]
    public async Task workflow_activate_and_workflow_pause_refuse_a_session_with_SND_AUTH_002()
    {
        await _rig.Seed();
        await using var services = Services();
        using var caller = EnterSession();

        var activate = await Call(services, HostWorkflowTools.Activate);
        var pause = await Call(services, HostWorkflowTools.Pause);

        Assert.All([activate, pause], static r => Assert.True(r.IsError));
        Assert.All([activate, pause], static r => Assert.Equal("SND-AUTH-002", Structured(r)["code"]!.GetValue<string>()));
    }
}
