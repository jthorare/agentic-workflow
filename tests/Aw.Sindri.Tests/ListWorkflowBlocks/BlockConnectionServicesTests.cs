using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Docs;
using Aw.Sindri.Features.ListWorkflowBlocks;
using Aw.Sindri.Tests.Toolkits;

namespace Aw.Sindri.Tests.ListWorkflowBlocks;

/// <summary>
/// GET /sindri/workflows/blocks (#3704): a field that names a connection says which services' connections fit it, so the step sheet's picker lists those and its button
/// says "Connect &lt;Service&gt;" from the block's own definition, not a GitHub rule in the app. An empty list is any connection; a field that is not a connection has none.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class BlockConnectionServicesTests
{
    private static readonly JsonSerializerOptions _web = new(JsonSerializerDefaults.Web);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_connection_events_connection_fits_every_service_with_pollable_events()
    {
        var connection = await Field("connection_event", "connection");

        Assert.Equal(["github", "linear", "slack", "google"], connection.Services!, StringComparer.Ordinal);   // why: a connection field carries its services; null fails the comparison
    }

    [Fact]
    public async Task A_toolkit_tools_connection_fits_any_connection()
    {
        var connection = await Field("call_tool", "connection");

        Assert.NotNull(connection.Services);
        Assert.Empty(connection.Services);
        Assert.Empty(JsonSerializer.SerializeToNode(connection, _web)!.AsObject()["services"]!.AsArray());   // why: a record serializes to an object, and an empty list is written as "services": []
    }

    [Fact]
    public async Task A_field_that_is_not_a_connection_leaves_services_out_of_the_json()
    {
        var other = await Field("connection_event", "event");

        Assert.Null(other.Services);
        Assert.False(JsonSerializer.SerializeToNode(other, _web)!.AsObject().ContainsKey("services"));   // why: a record serializes to an object
    }

    private static async Task<BlockFieldView> Field(string type, string name)
    {
        var catalog = new Catalog(new ConnectionEventBlock(), new CallToolBlock(new FakeRunningToolkits([]), new NoConnections()));
        var listed = await new ListWorkflowBlocksService(catalog, new Docs()).Handle(new ListWorkflowBlocksRequest(), Token);
        Assert.True(listed.IsSuccess);
        return listed.Value.Groups.SelectMany(static g => g.Blocks).Single(b => b.Type == type).Config.Single(f => f.Name == name);
    }

    private sealed class NoConnections : IConnectionToolPort
    {
        public ValueTask<Result<IReadOnlyList<ConnectionTool>>> ListTools(string connection, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Failure<IReadOnlyList<ConnectionTool>>(SindriErrors.ConnectionNotFound.With($"no connection '{connection}'")));

        public ValueTask<Result<string>> Call(string connection, string tool, string argumentsJson, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Failure<string>(SindriErrors.ConnectionNotFound.With($"no connection '{connection}'")));
    }

    private sealed class Catalog(params IGraphBlockType[] blocks) : IGraphBlockCatalog
    {
        public IReadOnlyDictionary<string, IGraphBlockType> Types { get; } = blocks.ToDictionary(static b => b.Type, StringComparer.Ordinal);
    }

    private sealed class Docs : IWorkflowDocs
    {
        public IReadOnlyList<WorkflowDocPage> Pages => [];
    }
}
