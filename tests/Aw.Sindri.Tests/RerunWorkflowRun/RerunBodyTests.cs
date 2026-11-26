using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Tests.RerunWorkflowRun;

/// <summary>The re-run route's body takes <c language="text">input</c> as a JSON object, the same shape the MCP tool workflow_rerun takes (#4205).</summary>
[Lane(TestLane.Unit)]
public sealed class RerunBodyTests
{
    // The route binds with the profile of AwJson (camelCase, case-insensitive; LoopbackBinding.CopyProfile), not the context's bare defaults.
    private static readonly JsonSerializerOptions _options = AwJson.CreateOptions(SindriJsonContext.Default);

    private static RerunWorkflowRunBody Read(string json) => JsonSerializer.Deserialize<RerunWorkflowRunBody>(json, _options)!;

    [Fact]
    public void Input_is_read_as_a_JSON_object()
    {
        var body = Read("{\"fromNode\":\"b\",\"input\":{\"repo\":\"web\"}}");

        var input = Assert.IsType<JsonObject>(body.Input);
        Assert.Equal("web", input["repo"]!.GetValue<string>());
    }

    [Fact]
    public void Input_may_be_left_out()
    {
        var body = Read("{\"fromNode\":\"b\"}");

        Assert.Null(body.Input);
    }

    [Fact]
    public void A_non_object_input_reads_as_text_the_service_refuses()
    {
        var body = Read("{\"fromNode\":\"b\",\"input\":[1]}");

        Assert.Equal("[1]", body.Input!.ToJsonString());
    }
}
