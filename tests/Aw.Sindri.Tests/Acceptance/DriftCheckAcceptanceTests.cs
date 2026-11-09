using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.ActivateWorkflow;
using Aw.Sindri.Features.ApplyWorkflowOps;
using Aw.Sindri.Features.CreateWorkflow;
using Aw.Sindri.Features.ValidateWorkflow;
using Aw.Sindri.Tests.WorkflowWrites;
using static Aw.Sindri.Tests.WorkflowWrites.WorkflowWriteRig;

namespace Aw.Sindri.Tests.Acceptance;

/// <summary>
/// Part 2's acceptance story (issue 2258, PR 8b), as far as the blocks that exist today go: "every weekday check whether the client still matches the API, and tell me when it doesn't" becomes a workflow
/// through the write slices only (create from a template, edit by ops, validate, activate), over the real block types and the made-up repos in tests/fixtures/graph/drift. The runs, the agent that reads the
/// two repos and the pull request it opens need the agent and forge blocks of PR 9 and later, so they are not part of this test yet.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class DriftCheckAcceptanceTests : IDisposable
{
    private readonly WorkflowWriteRig _rig = new(realBlocks: true);

    public void Dispose() => _rig.Dispose();

    [Theory]
    [InlineData("in-sync")]
    [InlineData("drifted")]
    public async Task A_sentence_becomes_an_active_workflow_through_the_write_slices(string client)
    {
        _ = await _rig.Harness.HoldLease();
        var prompt = Prompt(client);
        var create = new CreateWorkflowService(_rig.Workflows, _rig.Checker, _rig.Harness.Ids, _rig.Log);
        var edit = new ApplyWorkflowOpsService(_rig.Workflows, _rig.Checker, _rig.Log);
        var validate = new ValidateWorkflowService(_rig.Workflows, _rig.Checker);
        var activate = new ActivateWorkflowService(_rig.Workflows, _rig.Checker, _rig.Log);

        var made = await create.Handle(new CreateWorkflowRequest(Slug, "weekday-check", Spec: null, "session:agent"), Token);
        var ops = "[{\"op\":\"set_config\",\"node\":\"check\",\"path\":\"prompt\",\"value\":" + System.Text.Json.JsonSerializer.Serialize(prompt) + "}]";
        var edited = await edit.Handle(new ApplyWorkflowOpsRequest(Slug, made.Value.Revision, ops, "session:agent"), Token);
        var checkedOk = await validate.Handle(new ValidateWorkflowRequest(Slug, Revision: null, Spec: null), Token);
        var on = await activate.Handle(new ActivateWorkflowRequest(Slug, Revision: null, "session:agent"), Token);

        Assert.True(made.IsSuccess, made.IsError ? made.Error.Message : null);
        Assert.True(edited.IsSuccess, edited.IsError ? edited.Error.Message : null);
        Assert.Equal(2, edited.Value.Revision);
        Assert.True(checkedOk.Value.Passed);
        Assert.Equal(2, on.Value.Revision);
        var record = (await _rig.Workflows.Get(made.Value.Id, Token)).Value;
        Assert.Equal((2, 2), (record.ActiveRevision, record.LatestRevision));
        var saved = (await _rig.Workflows.GetRevision(made.Value.Id, 2, Token)).Value;
        var check = saved.Spec.Nodes.Single(static n => n.Id.Value == "check");
        Assert.Contains("currency", check.Config["prompt"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(client == "drifted", !check.Config["prompt"]!.GetValue<string>().Contains("currency: string", StringComparison.Ordinal));
        Assert.Equal(
            [EventTypes.WorkflowCreated, EventTypes.WorkflowRevised, EventTypes.WorkflowActivated],
            _rig.Harness.Store.Events.Select(static e => e.Envelope.Type).Where(static t => t.StartsWith("workflow.", StringComparison.Ordinal)),
            StringComparer.Ordinal);
    }

    private static string Prompt(string client)
    {
        var root = Root();
        var contract = File.ReadAllText(Path.Combine(root, "api-repo", "openapi.json"));
        var types = File.ReadAllText(Path.Combine(root, "client-repo", client, "api.ts"));
        return $"Compare the API contract with the client's types and say whether the client drifted. Set flagged to true when a field is missing or wrong, and summarize why.\n\nContract:\n{contract}\n\nClient:\n{types}";
    }

    private static string Root()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "tests", "fixtures", "graph", "drift");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("tests/fixtures/graph/drift is not above " + AppContext.BaseDirectory);
    }
}
