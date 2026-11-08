using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>LN-WF5: the pack a Step bundles rides on the launch request to the launcher, which copies it into the Step directory.</summary>
public sealed class PackBundleLaunchTests
{
    private static readonly AgentStepTarget _target = new(ItemId.From("ITM-" + new string('a', 32)), AgentId.From("claude-code"), "/repo");

    private static AgentStepRun Run()
    {
        var address = InstanceAddress.From("T-1/step/1");
        return new AgentStepRun(WorkflowInstanceId.Of(address), address, RunToken.From("1.1"), new StepRef("draft", "step.design.draft"), WriteScope: true);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_bundle_is_on_the_Steps_arguments()
    {
        var bundle = new PackBundle("style", LensPackId.From("plain"), "sha256:" + new string('b', 64), new Dictionary<string, string>(StringComparer.Ordinal) { ["pack.json"] = "{}" });

        var request = AgentLaunches.RequestFor(Run(), _target, descriptor: null, new AgentLaunchExtras(Bundle: bundle));

        Assert.Same(bundle, request.Step?.Bundle);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_Step_without_a_bundle_carries_none()
    {
        var request = AgentLaunches.RequestFor(Run(), _target);

        Assert.Null(request.Step?.Bundle);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_fact_files_are_on_the_Steps_arguments_and_the_brief_names_them_in_one_line()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal) { ["workspace.json"] = "\"/work\"", ["docsScope.json"] = "{}" };

        var request = AgentLaunches.RequestFor(Run(), _target, descriptor: null, new AgentLaunchExtras(Facts: files));

        Assert.Same(files, request.Step?.Facts);
        var lines = request.Step!.Brief!.Split('\n').Where(l => l.Contains("docsScope.json", StringComparison.Ordinal)).ToArray();
        var line = Assert.Single(lines);
        Assert.Contains("$AW_SINDRI_RUN_DIR", line, StringComparison.Ordinal);
        Assert.Contains("workspace.json", line, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_Step_with_no_fact_files_carries_none_and_its_brief_has_no_such_line()
    {
        var request = AgentLaunches.RequestFor(Run(), _target);

        Assert.Null(request.Step?.Facts);
        Assert.DoesNotContain(".json", request.Step!.Brief!, StringComparison.Ordinal);
    }
}
