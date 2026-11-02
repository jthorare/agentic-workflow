using System.Text;
using Aw.Context.Core.ScopeGraph;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Kernel.Json;

namespace Aw.Context.Infrastructure.ScopeRuns;

/// <summary>The scope-run store in context.db (issue 1005): an instance's graph with its node ids, and the reads of its runs. Every call migrates first, like the graph store's writes.</summary>
internal sealed class SqliteScopeRunStore(ContextDatabase database) : IScopeRunStore
{
    private const string UpsertGraph = "INSERT INTO scope_graph (instance, map_version, graph, at) VALUES ($instance, $mapVersion, $graph, $at) "
        + "ON CONFLICT (instance) DO UPDATE SET map_version = excluded.map_version, graph = excluded.graph, at = excluded.at";
    private const string DeleteNodes = "DELETE FROM scope_graph_node WHERE instance = $instance";
    private const string InsertNode = "INSERT OR IGNORE INTO scope_graph_node (instance, id) VALUES ($instance, $id)";
    private const string HasGraph = "SELECT count(*) FROM scope_graph WHERE instance = $instance";
    private const string HasNode = "SELECT count(*) FROM scope_graph_node WHERE instance = $instance AND id = $id";
    private const string InsertRead = "INSERT INTO scope_read (instance, run, node, tool, outside, reason, at) VALUES ($instance, $run, $node, $tool, $outside, $reason, $at)";
    private const string SelectReads = "SELECT node, tool, outside, reason, at FROM scope_read WHERE instance = $instance AND run = $run ORDER BY id";

    private static readonly System.Text.Json.JsonSerializerOptions _json = AwJson.CreateOptions(ScopeRunJsonContext.Default);

    public async ValueTask<Result<bool>> Remember(string instance, ScopeGraphView graph, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var json = Encoding.UTF8.GetString(AwJson.Serialize(graph, _json));
        List<SqlStatement> statements =
        [
            new(UpsertGraph, c => c.Bind("$instance", instance).Bind("$mapVersion", graph.MapVersion).Bind("$graph", json).Bind("$at", at)),
            new(DeleteNodes, c => c.Bind("$instance", instance)),
            .. graph.Nodes.Select(n => new SqlStatement(InsertNode, c => c.Bind("$instance", instance).Bind("$id", n.Id.Value))),
        ];
        return await Run(statements, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<bool?>> Holds(string instance, string node, CancellationToken cancellationToken)
    {
        var open = await database.Writable(cancellationToken).ConfigureAwait(false);
        if (open.IsError)
        {
            return open.Error;
        }

        var begun = await open.Value.BeginSession(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return ContextDatabase.Translate(begun.Error);
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var graphs = await SqliteQueries.One(begun.Value, new SqlStatement(HasGraph, c => c.Bind("$instance", instance)), ScopeRunRows.Count, cancellationToken).ConfigureAwait(false);
            if (graphs.IsError)
            {
                return ContextDatabase.Translate(graphs.Error);
            }

            if (graphs.Value == 0)
            {
                return Results.Success<bool?>(value: null);
            }

            var nodes = await SqliteQueries.One(begun.Value, new SqlStatement(HasNode, c => c.Bind("$instance", instance).Bind("$id", node)), ScopeRunRows.Count, cancellationToken).ConfigureAwait(false);
            return nodes.IsError ? ContextDatabase.Translate(nodes.Error) : Results.Success<bool?>(nodes.Value > 0);
        }
    }

    public async ValueTask<Result<bool>> Log(string instance, string run, GraphRead read, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(read);
        return await Run([new SqlStatement(InsertRead, c => c
            .Bind("$instance", instance).Bind("$run", run).Bind("$node", read.Node).Bind("$tool", read.Tool).Bind("$outside", read.Outside).Bind("$reason", read.Reason).Bind("$at", read.At))], cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<IReadOnlyList<GraphRead>>> Reads(string instance, string run, CancellationToken cancellationToken)
    {
        var open = await database.Writable(cancellationToken).ConfigureAwait(false);
        if (open.IsError)
        {
            return open.Error;
        }

        var begun = await open.Value.BeginSession(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return ContextDatabase.Translate(begun.Error);
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var rows = await SqliteQueries.List(begun.Value, new SqlStatement(SelectReads, c => c.Bind("$instance", instance).Bind("$run", run)), ScopeRunRows.Read, cancellationToken).ConfigureAwait(false);
            return rows.IsError ? ContextDatabase.Translate(rows.Error) : rows;
        }
    }

    private async ValueTask<Result<bool>> Run(IReadOnlyList<SqlStatement> statements, CancellationToken cancellationToken)
    {
        var open = await database.Writable(cancellationToken).ConfigureAwait(false);
        if (open.IsError)
        {
            return open.Error;
        }

        var begun = await open.Value.BeginSession(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return ContextDatabase.Translate(begun.Error);
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            foreach (var statement in statements)
            {
                var step = await SqliteQueries.Execute(begun.Value, statement, cancellationToken).ConfigureAwait(false);
                if (step.IsError)
                {
                    return ContextDatabase.Translate(step.Error);
                }
            }

            var committed = await begun.Value.Commit(cancellationToken).ConfigureAwait(false);
            return committed.IsError ? ContextDatabase.Translate(committed.Error) : true;
        }
    }
}
