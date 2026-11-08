using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Google.Protobuf;
using Temporalio.Api.Common.V1;
using Temporalio.Converters;

namespace Aw.Sindri.Tests.Launch;

/// <summary>The node run a launch belongs to travels on the request through Temporal's payload serializer, at the end, and old payloads without it still read.</summary>
public sealed class LaunchRequestNodeTests
{
    private static readonly IPayloadConverter _converter = DataConverter.Default.PayloadConverter;
    private static readonly NodeRunAddress _address = new(WorkflowRunId.Of(WorkflowSlug.From("nightly"), 3, 12), NodeId.From("agent"), Index: 2);

    private static LaunchRequest Request(NodeRunAddress? run, AgentTask? task) =>
        new(LaunchId.From("LCH-00000000000000000000000000000001"), ItemId.From("ITM-" + new string('a', 32)), AgentId.From("claude-code"), LaunchMode.Open, Attended: false, "/work/app", Instance: null, Worktree: null, Step: null, Run: null, NodeRun: run, Task: task);

    [Fact, Trait("Lane", "unit")]
    public void A_request_with_a_node_run_and_task_round_trips_and_keeps_them_at_the_end()
    {
        var task = new AgentTask("check drift", new Dictionary<string, string>(StringComparer.Ordinal) { ["drifted"] = "boolean" }, Model: null);

        var payload = _converter.ToPayload(Request(_address, task));
        var back = _converter.ToValue<LaunchRequest>(payload);

        Assert.Equal(_address, back.NodeRun);
        Assert.Equal("check drift", back.Task?.Brief);
        Assert.Equal("boolean", back.Task?.Outputs["drifted"]);
        var names = JsonNode.Parse(payload.Data.ToStringUtf8())!.AsObject().Select(p => p.Key).ToList();   // why: the test parses its own output
        Assert.Equal(["NodeRun", "Task"], names.TakeLast(2), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Old_json_without_nodeRun_still_reads_with_a_null_run()
    {
        var json = JsonNode.Parse(_converter.ToPayload(Request(_address, new AgentTask("b", new Dictionary<string, string>(StringComparer.Ordinal), Model: null))).Data.ToStringUtf8())!.AsObject();   // why: the test parses its own output
        _ = json.Remove("NodeRun");
        _ = json.Remove("Task");
        var old = new Payload { Data = ByteString.CopyFromUtf8(json.ToJsonString()) };
        old.Metadata["encoding"] = ByteString.CopyFromUtf8("json/plain");

        var back = _converter.ToValue<LaunchRequest>(old);

        Assert.Null(back.NodeRun);
        Assert.Null(back.Task);
        Assert.Equal("/work/app", back.RepoPath);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_attended_request_has_no_node_run()
    {
        var attended = Request(run: null, task: null) with { Attended = true };

        Assert.Null(_converter.ToValue<LaunchRequest>(_converter.ToPayload(attended)).NodeRun);
    }
}
