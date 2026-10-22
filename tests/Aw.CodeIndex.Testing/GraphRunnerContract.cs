using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Ports;
using Xunit;

namespace Aw.CodeIndex.Testing;

/// <summary>
/// What every <see cref="IGraphRunner"/> honours (spec §5.5, §11): a tool that names a node outside the snapshot has that node and its edges
/// dropped, the kept edges join kept nodes, and a tool that is missing is a layer code (007 to 009), never an exception. A derived class binds it to the
/// fake and to the real adapter and carries the lane.
/// </summary>
public abstract class GraphRunnerContract
{
    /// <summary>The files the request carries.</summary>
    protected static readonly string[] RequestPaths = ["src/a.ts", "src/b.ts"];

    /// <summary>The key of the node for 'f' in src/a.ts, which calls 'g' in src/b.ts at line 4.</summary>
    protected const string CallerKey = "src_a_f";

    /// <summary>The key of the node for 'g' in src/b.ts.</summary>
    protected const string CalleeKey = "src_b_g";

    /// <summary>The key of a node the tool names in a file the request does not carry, with an edge from the caller to it.</summary>
    protected const string OutsiderKey = "src_x_h";

    /// <summary>A runner whose tool names f, g and an outsider h (file src/x.ts, not in <see cref="RequestPaths"/>), with the calls f to g (line 4) and f to h.</summary>
    /// <returns>The runner and the request to run it with.</returns>
    protected abstract (IGraphRunner Runner, GraphRequest Request) ArrangeWithOutsider();

    /// <summary>A runner whose tool is missing.</summary>
    /// <returns>The runner and a request.</returns>
    protected abstract (IGraphRunner Runner, GraphRequest Request) ArrangeMissingTool();

    /// <summary>The outsider and its edge are dropped; the nodes and the edge inside the snapshot stay.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_node_outside_the_snapshot_is_dropped_with_its_edges()
    {
        var (runner, request) = ArrangeWithOutsider();

        var run = await runner.Run(request, TestContext.Current.CancellationToken);

        Require(run.IsSuccess, "the run succeeds");
        var graph = run.Value;
        Require(graph.Nodes.All(n => n.File is { } f && RequestPaths.Contains(f)), "every node names a file of the request");
        Require(graph.Nodes.Select(n => n.NodeKey).Order(StringComparer.Ordinal).SequenceEqual([CallerKey, CalleeKey], StringComparer.Ordinal), "the callable nodes inside the snapshot stay");
        var edge = graph.Edges.Single();
        Require(edge.Source == CallerKey && edge.Target == CalleeKey && edge.Line == 4, "the call between kept nodes stays, with its line");
    }

    /// <summary>A missing tool is a layer code, not an exception.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_missing_tool_is_a_layer_code_and_never_an_exception()
    {
        var (runner, request) = ArrangeMissingTool();

        var probe = await runner.Probe(TestContext.Current.CancellationToken);
        var run = await runner.Run(request, TestContext.Current.CancellationToken);

        string[] codes = [CodeIndexErrors.NoSandbox.Code, CodeIndexErrors.GraphUnavailable.Code, CodeIndexErrors.PinMismatch.Code];
        Require(probe.IsError && codes.Contains(probe.Error.Code), "the probe answers a layer code");
        Require(run.IsError && codes.Contains(run.Error.Code), "the run answers a layer code");
    }

    private static void Require(bool condition, string what) => Expect.That(condition, what);
}
