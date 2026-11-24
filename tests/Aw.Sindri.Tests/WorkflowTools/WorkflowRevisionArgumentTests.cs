using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.GetWorkflow;
using Aw.Sindri.Features.RunWorkflow;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Mcp;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Tests.WorkflowTools;

/// <summary>workflow_get's revision argument (#3693): a number or 'latest' builds the same read as GET /sindri/workflows/{slug}?revision=, and a bad one is refused before the read. workflow_run's (#3691): a whole number of 1 or more, passed on to the run.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowRevisionArgumentTests
{
    private const string ArgumentsInvalid = "SND-CONTEXT-022";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Dictionary<string, JsonElement> Arguments(params (string Name, object Value)[] pairs) =>
        pairs.ToDictionary(static p => p.Name, static p => JsonSerializer.SerializeToElement(p.Value), StringComparer.Ordinal);

    private static JsonNode Structured(CallToolResult result) => JsonNode.Parse(result.StructuredContent!.Value.GetRawText())!;   // why: every tool result carries structuredContent

    private static ServiceProvider Services(Reads reads) =>
        new ServiceCollection().AddSingleton(AwJson.CreateOptions(SindriJsonContext.Default)).AddSingleton<IService<GetWorkflowRequest, GetWorkflowResponse>>(reads).BuildServiceProvider();

    [Fact]
    public async Task A_number_reads_that_revision()
    {
        var reads = new Reads();
        await using var services = Services(reads);

        _ = await WorkflowReadTools.Call(WorkflowReadTools.Get, services, Arguments(("slug", "morning-check"), ("revision", 2)), Token);

        var asked = Assert.Single(reads.Asked);
        Assert.Equal(("morning-check", 2, false), (asked.Slug.Value, asked.Revision, asked.Newest));
    }

    [Fact]
    public async Task Latest_reads_the_newest_revision()
    {
        var reads = new Reads();
        await using var services = Services(reads);

        _ = await WorkflowReadTools.Call(WorkflowReadTools.Get, services, Arguments(("slug", "morning-check"), ("revision", "latest")), Token);

        var asked = Assert.Single(reads.Asked);
        Assert.Equal((null, true), (asked.Revision, asked.Newest));
    }

    [Fact]
    public async Task No_revision_reads_the_one_the_workflow_runs()
    {
        var reads = new Reads();
        await using var services = Services(reads);

        _ = await WorkflowReadTools.Call(WorkflowReadTools.Get, services, Arguments(("slug", "morning-check")), Token);

        var asked = Assert.Single(reads.Asked);
        Assert.Equal((null, false), (asked.Revision, asked.Newest));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("Latest")]
    public async Task A_revision_text_the_route_refuses_is_the_routes_invalid_input(string revision)
    {
        var reads = new Reads();
        await using var services = Services(reads);

        var result = await WorkflowReadTools.Call(WorkflowReadTools.Get, services, Arguments(("slug", "morning-check"), ("revision", revision)), Token);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.InvalidInput.Code, Structured(result)["code"]!.GetValue<string>());
        Assert.Empty(reads.Asked);
    }

    [Fact]
    public async Task A_fraction_is_invalid_input()
    {
        var reads = new Reads();
        await using var services = Services(reads);

        var result = await WorkflowReadTools.Call(WorkflowReadTools.Get, services, Arguments(("slug", "morning-check"), ("revision", 2.5)), Token);

        Assert.True(result.IsError);
        Assert.Empty(reads.Asked);
    }

    [Fact]
    public async Task A_revision_that_is_neither_a_number_nor_text_is_an_invalid_argument()
    {
        var reads = new Reads();
        await using var services = Services(reads);

        var result = await WorkflowReadTools.Call(WorkflowReadTools.Get, services, Arguments(("slug", "morning-check"), ("revision", true)), Token);

        Assert.True(result.IsError);
        Assert.Equal(ArgumentsInvalid, Structured(result)["code"]!.GetValue<string>());
        Assert.Empty(reads.Asked);
    }

    [Fact]
    public void The_input_schema_names_revision_and_still_needs_only_slug()
    {
        var tool = Assert.Single(WorkflowReadTools.All, static t => t.Name == WorkflowReadTools.Get);
        var schema = JsonNode.Parse(tool.InputSchema.GetRawText())!;

        Assert.NotNull(schema["properties"]?["revision"]);
        Assert.Equal(["slug"], schema["required"]!.AsArray().Select(static r => r!.GetValue<string>()), StringComparer.Ordinal);
    }

    [Fact]
    public async Task workflow_run_passes_the_revision_on()
    {
        var runs = new Runs();
        await using var services = RunServices(runs);

        _ = await Host.Mcp.WorkflowTools.Call(Host.Mcp.WorkflowTools.Run, services, Arguments(("slug", "morning-check"), ("revision", 2)), Token);

        Assert.Equal(2, Assert.Single(runs.Asked).Revision);
    }

    [Fact]
    public async Task workflow_run_without_a_revision_runs_the_live_one()
    {
        var runs = new Runs();
        await using var services = RunServices(runs);

        _ = await Host.Mcp.WorkflowTools.Call(Host.Mcp.WorkflowTools.Run, services, Arguments(("slug", "morning-check")), Token);

        Assert.Null(Assert.Single(runs.Asked).Revision);
    }

    [Theory]
    [InlineData(0)]
    [InlineData("latest")]
    [InlineData(4_294_967_297L)]
    public async Task workflow_run_refuses_a_revision_that_is_not_a_whole_number_of_1_or_more(object revision)
    {
        var runs = new Runs();
        await using var services = RunServices(runs);

        var result = await Host.Mcp.WorkflowTools.Call(Host.Mcp.WorkflowTools.Run, services, Arguments(("slug", "morning-check"), ("revision", revision)), Token);

        Assert.True(result.IsError);
        Assert.Equal(ArgumentsInvalid, Structured(result)["code"]!.GetValue<string>());
        Assert.Empty(runs.Asked);
    }

    [Fact]
    public void workflow_run_input_schema_names_revision()
    {
        var tool = Assert.Single(Host.Mcp.WorkflowTools.All, static t => t.Name == Host.Mcp.WorkflowTools.Run);

        Assert.NotNull(JsonNode.Parse(tool.InputSchema.GetRawText())!["properties"]?["revision"]);
    }

    private static ServiceProvider RunServices(Runs runs) =>
        new ServiceCollection().AddSingleton(AwJson.CreateOptions(SindriJsonContext.Default))
            .AddSingleton<ICallerContextAccessor>(new AsyncLocalCallerContextAccessor(new SequentialIdGenerator()))
            .AddSingleton<IService<RunWorkflowRequest, RunWorkflowResponse>>(runs).BuildServiceProvider();

    /// <summary>Records what it was asked and answers that no workflow exists.</summary>
    private sealed class Runs : IService<RunWorkflowRequest, RunWorkflowResponse>
    {
        public List<RunWorkflowRequest> Asked { get; } = [];

        public ValueTask<Result<RunWorkflowResponse>> Handle(RunWorkflowRequest request, CancellationToken cancellationToken)
        {
            Asked.Add(request);
            return ValueTask.FromResult<Result<RunWorkflowResponse>>(SindriErrors.GraphNotFound.With($"no workflow '{request.Slug.Value}'"));
        }
    }

    /// <summary>Records what it was asked and answers that no workflow exists, so the call needs no store.</summary>
    private sealed class Reads : IService<GetWorkflowRequest, GetWorkflowResponse>
    {
        public List<GetWorkflowRequest> Asked { get; } = [];

        public ValueTask<Result<GetWorkflowResponse>> Handle(GetWorkflowRequest request, CancellationToken cancellationToken)
        {
            Asked.Add(request);
            return ValueTask.FromResult<Result<GetWorkflowResponse>>(SindriErrors.GraphNotFound.With($"no workflow '{request.Slug.Value}'"));
        }
    }
}
