using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Templates;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Features.ActivateWorkflow;
using Aw.Sindri.Features.ApplyWorkflowOps;
using Aw.Sindri.Features.CreateWorkflow;
using Aw.Sindri.Features.ValidateWorkflow;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Mcp;
using Aw.Sindri.Tests.WorkflowWrites;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using static Aw.Sindri.Tests.WorkflowWrites.WorkflowWriteRig;

namespace Aw.Sindri.Tests.Acceptance;

/// <summary>
/// Part 2's acceptance story (issue 2258, PR 8b), as far as the blocks that exist today go: "every weekday check whether the client still matches the API, and tell me when it doesn't" becomes a workflow
/// through the workflow MCP tools only (create from a template, edit by ops, validate, activate), called as a launched agent session, over the real block types.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class DriftCheckAcceptanceTests : IDisposable
{
    private const string Prompt = "Compare the API contract with the client's types and say whether the client drifted. Set flagged to true when a field is missing or wrong, and summarize why.";

    private readonly WorkflowWriteRig _rig = new(realBlocks: true);

    public void Dispose() => _rig.Dispose();

    // why: the runs, the agent that reads the two repos and the pull request it opens need the agent and forge blocks of PR 9; that half moves to a follow-up after PR 9 (#2453)
    [Fact]
    public async Task A_launched_session_makes_an_active_workflow_through_the_mcp_tools_and_every_write_names_it()
    {
        _ = await _rig.Harness.HoldLease();
        var launch = LaunchId.From("LCH-" + new string('0', 31) + "1");
        var actor = "session:launch:" + launch.Value;
        await using var services = Services();
        using var caller = _rig.Harness.Callers.Enter(new CallerContext(CallerClass.Session, Terminal: false, actor, _rig.Harness.Ids.New<CommandId>(), _rig.Harness.Ids.New<CorrelationId>(), Repo: null, launch));

        var made = await Call(services, WorkflowTools.Create, ("slug", Slug.Value), ("template", "weekday-check"));
        var edited = await Call(services, WorkflowTools.ApplyOps, ("slug", Slug.Value), ("baseRevision", 1), ("ops", new object[] { new { op = "set_config", node = "check", path = "prompt", value = Prompt } }));
        var checkedOk = await Call(services, WorkflowTools.Validate, ("slug", Slug.Value));
        var checkedBad = await Call(services, WorkflowTools.Validate, ("spec", BrokenSpec()));
        var on = await Call(services, WorkflowTools.Activate, ("slug", Slug.Value));

        Assert.All(new[] { made, edited, checkedOk, checkedBad, on }, static r => Assert.True(r.IsError != true, r.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text));
        Assert.Equal(2, Structured(edited)["revision"]!.GetValue<int>());
        Assert.True(Structured(checkedOk)["passed"]!.GetValue<bool>());
        Assert.Equal(2, Structured(on)["revision"]!.GetValue<int>());
        var record = (await _rig.Workflows.List(includeArchived: false, Token)).Value.Single(r => r.Slug == Slug);
        Assert.Equal((2, 2, actor), (record.ActiveRevision, record.LatestRevision, record.CreatedBy));
        var saved = (await _rig.Workflows.GetRevision(record.Id, 2, Token)).Value;
        Assert.Equal(Prompt, saved.Spec.Nodes.Single(static n => n.Id.Value == "check").Config["prompt"]!.GetValue<string>());

        var changes = _rig.Harness.Store.Events.Where(static e => e.Envelope.Type.StartsWith("workflow.", StringComparison.Ordinal)).ToList();
        Assert.Equal([EventTypes.WorkflowCreated, EventTypes.WorkflowRevised, EventTypes.WorkflowActivated], changes.Select(static e => e.Envelope.Type), StringComparer.Ordinal);
        Assert.All(changes, e => Assert.Equal(actor, JsonNode.Parse(e.Envelope.Payload)!.AsObject().Single(static p => string.Equals(p.Key, "by", StringComparison.OrdinalIgnoreCase)).Value!.GetValue<string>()));

        var findings = Structured(checkedBad)["findings"]!.AsArray();
        Assert.NotEmpty(findings);
        Assert.All(findings, static f => Assert.StartsWith("<untrusted source=\"sindri:workflow\">", f!["message"]!.GetValue<string>(), StringComparison.Ordinal));
    }

    private static string BrokenSpec()
    {
        var spec = JsonNode.Parse(GraphJson.Write(GraphTemplates.Find("weekday-check")!.Spec))!;
        spec["nodes"]![0]!["type"] = "no_such_block";
        return spec.ToJsonString();
    }

    private static JsonNode Structured(CallToolResult result) => JsonNode.Parse(result.StructuredContent!.Value.GetRawText())!;   // why: every tool result carries structuredContent

    private static ValueTask<CallToolResult> Call(IServiceProvider services, string tool, params (string Name, object Value)[] pairs) =>
        WorkflowTools.Call(tool, services, pairs.ToDictionary(static p => p.Name, static p => JsonSerializer.SerializeToElement(p.Value), StringComparer.Ordinal), Token);

    private ServiceProvider Services() => new ServiceCollection()
        .AddSingleton(AwJson.CreateOptions(SindriJsonContext.Default))
        .AddSingleton(_rig.Harness.Callers)
        .AddSingleton<IService<CreateWorkflowRequest, CreateWorkflowResponse>>(new CreateWorkflowService(_rig.Workflows, _rig.Checker, _rig.Harness.Ids, _rig.Log))
        .AddSingleton<IService<ApplyWorkflowOpsRequest, ApplyWorkflowOpsResponse>>(new ApplyWorkflowOpsService(_rig.Workflows, _rig.Checker, _rig.Log))
        .AddSingleton<IService<ValidateWorkflowRequest, ValidateWorkflowResponse>>(new ValidateWorkflowService(_rig.Workflows, _rig.Checker))
        .AddSingleton<IService<ActivateWorkflowRequest, ActivateWorkflowResponse>>(new ActivateWorkflowService(_rig.Workflows, _rig.Checker, _rig.Log))
        .BuildServiceProvider();
}
