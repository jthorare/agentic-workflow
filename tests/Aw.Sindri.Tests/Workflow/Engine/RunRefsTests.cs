using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>The run ref and the run folder (sandbox spec SB5, SB16(4), SB17).</summary>
[Lane(TestLane.Unit)]
public sealed class RunRefsTests
{
    private static readonly WorkflowInstanceId _instance = WorkflowInstanceId.Of(InstanceAddress.From("T-3/step/1"));

    [Fact]
    public void The_run_branch_is_sindri_instance_run_work()
    {
        Expect.Equal($"sindri/{_instance.Value}/2.1", RunRefs.Namespace(_instance, RunToken.From("2.1")));
        Expect.Equal($"sindri/{_instance.Value}/2.1/work", RunRefs.Branch(_instance, RunToken.From("2.1")));
    }

    [Fact]
    public void Two_instances_on_one_item_never_share_a_ref()
    {
        var other = WorkflowInstanceId.Of(InstanceAddress.From("T-3/step/2"));

        Expect.True(!string.Equals(RunRefs.Branch(_instance, RunToken.From("1.1")), RunRefs.Branch(other, RunToken.From("1.1")), StringComparison.Ordinal));
    }

    [Fact]
    public void The_run_folder_is_per_instance_step_and_run_under_the_home()
    {
        var folder = RunFolders.PathFor("/home/thor/.sindri", _instance, "produce", RunToken.From("1.2"));

        Expect.Equal(Path.Combine("/home/thor/.sindri", "workflow", "runs", _instance.Value, "produce", "1.2"), folder);
        Expect.True(!string.Equals(folder, RunFolders.PathFor("/home/thor/.sindri", _instance, "produce", RunToken.From("1.3")), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("result.json", true)]
    [InlineData("out/report.md", true)]
    [InlineData("out/../report.md", true)]
    [InlineData("../other/report.md", false)]
    [InlineData("out/../../report.md", false)]
    [InlineData("/etc/passwd", false)]
    [InlineData("", false)]
    public void A_report_output_must_resolve_inside_the_folder(string name, bool inside) =>
        Assert.Equal(inside, RunFolders.Contains("/home/thor/.sindri/workflow/runs/w/produce/1.1", name));
}
