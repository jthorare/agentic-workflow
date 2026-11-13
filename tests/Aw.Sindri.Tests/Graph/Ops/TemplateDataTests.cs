using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Expressions;
using Aw.Sindri.Core.Graph.Schedule;
using Aw.Sindri.Core.Graph.Templates;
using Aw.Sindri.Core.Graph.Validation;
using Aw.Sindri.Features.CreateWorkflow;
using Aw.Sindri.Tests.Graph.Blocks;
using Aw.Sindri.Tests.WorkflowWrites;
using static Aw.Sindri.Tests.WorkflowWrites.WorkflowWriteRig;

namespace Aw.Sindri.Tests.Graph.Ops;

/// <summary>#3509: the Weekday check and Daily digest starters need no outside data: no agent step (every sandbox Step denies `gh`), and the prompt reads the schedule's date.</summary>
[Lane(TestLane.Unit)]
public sealed class TemplateDataTests
{
    private static GraphValidator RealValidator() => GraphFixtures.Validator(
        blocks: GraphBlockRegistry.Standard(new BlockFakes.RecordingNotifyPort(), new BlockFakes.ScriptedModelPort("{}"), more: [new CreateItemBlock(new FlowFakes.RecordingItemPort()), new CodeBlock(runner: null!)]).Types,
        cron: (cron, zone) => CronExpression.TryParse(cron, zone, out _, out var problem) ? null : problem,
        condition: text => ConditionExpression.TryParse(text, out var expression, out var error) ? new ConditionParse(Error: null, expression!.References) : new ConditionParse(error, []));

    private static GraphSpec Built(string name) => GraphTemplates.Find(name)!.Spec;

    [Theory]
    [InlineData("weekday-check")]
    [InlineData("daily-digest")]
    public void The_template_has_no_agent_step(string name)
        => Assert.DoesNotContain(Built(name).Nodes, static n => n.Type == "run_agent");

    [Theory]
    [InlineData("weekday-check")]
    [InlineData("daily-digest")]
    public void The_prompt_reads_the_schedule_date_and_the_spec_validates_without_a_prompt_warning(string name)
    {
        var spec = Built(name);

        var trigger = Assert.Single(spec.Nodes, static n => n.Type == "schedule");
        var question = Assert.Single(spec.Nodes, static n => n.Type == "prompt").Config["prompt"]!.GetValue<string>();
        Assert.Contains("{{" + trigger.Id.Value + ".scheduledFor}}", question, StringComparison.Ordinal);
        var result = RealValidator().Validate(spec, GraphMode.Activate);
        Assert.True(result.Passed, string.Join("; ", result.Blocking));
        Assert.DoesNotContain(result.Findings, static f => f.ToString().Contains("GRAPH-PROMPT-001", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("weekday-check")]
    [InlineData("daily-digest")]
    public async Task Creating_the_template_needs_no_repo_in_the_profile(string name)
    {
        using var rig = new WorkflowWriteRig(realBlocks: true);
        var service = new CreateWorkflowService(rig.Workflows, rig.Checker, rig.Harness.Ids, rig.Log);
        _ = await rig.Harness.HoldLease();

        var result = await service.Handle(new CreateWorkflowRequest(Slug, name, Spec: null, "session:s1"), Token);

        Assert.True(result.IsSuccess);
    }
}
