using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>Issue 2985: the sandbox plans every run namespace a producer makes, a graph node's 'wf/&lt;slug&gt;/&lt;short run&gt;' included, and still refuses a malformed one.</summary>
[Lane(TestLane.Unit)]
public sealed class NodeRunNamespacePlanTests
{
    private static NodeRunAddress Node => NodeRunAddress.Of(WorkflowRunId.From("nightly-gate@2:2"), NodeId.From("gate"));

    private static bool Planned(string ns, out string? code)
    {
        var result = SandboxPlanner.Build(SandboxMechanism.PassThrough, Req.WriteScope with { RunNamespace = ns });
        code = result.IsError ? result.Error.Code : null;
        return !result.IsError;
    }

    [Fact]
    public void A_graph_node_namespace_is_planned()
    {
        var ns = Node.RefNamespace;

        var result = SandboxPlanner.Build(SandboxMechanism.PassThrough, Req.WriteScope with { RunNamespace = ns });

        Assert.False(result.IsError, result.IsError ? result.Error.Code + " " + result.Error.Message : null);
        Assert.Contains(result.Value.Mounts, m => m.Kind == PlanMountKind.ReadWrite && m.Path.EndsWith("/refs/heads/" + ns, StringComparison.Ordinal));
    }

    [Theory]
    // The attended open's form is AttendedOpen.RunNamespace(launch) = "sindri/open/<launch id>"; a literal here because AttendedOpen is internal to Infrastructure.
    [InlineData("sindri/open/LCH-0192f0c1a2b37c4d8e9f00112233aabb")]
    [InlineData("sindri/inst-0192f0c1/7")]
    [InlineData("wf/nightly-gate/2-2-gate")]
    public void Every_producer_shape_is_planned(string ns) => Assert.True(Planned(ns, out var code), code);

    [Fact]
    public void The_node_form_is_the_one_a_real_address_makes()
    {
        Assert.Equal("wf/nightly-gate/2-2-gate", Node.RefNamespace);
        Assert.True(Planned(Node.RefNamespace, out var code), code);
    }

    [Theory]
    [InlineData("wf/../x")]
    [InlineData("wf/a/b.lock")]
    [InlineData("wf/a")]
    [InlineData("wf/a/b/c")]
    [InlineData("other/a/b")]
    [InlineData("wf//b")]
    public void A_bad_shape_stays_refused(string ns)
    {
        Assert.False(Planned(ns, out var code));
        Assert.Equal(SindriErrors.LaunchUnconfinable.Code, code);
    }
}
