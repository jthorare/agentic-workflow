using System.Text.Json;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Proposals;
using Aw.Context.Core.Sources;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Context.Infrastructure.Graph;

/// <summary>The graph store's row mappers, one per SELECT in <see cref="GraphSql"/>.</summary>
internal static class GraphRows
{
    public static readonly IRowMapper<long> Id = new IdMapper();
    public static readonly IRowMapper<MapVersion> Version = new VersionMapper();
    public static readonly IRowMapper<GraphNode> Node = new NodeMapper();
    public static readonly IRowMapper<(string Owner, EvidenceRef Evidence)> Evidence = new EvidenceMapper();
    public static readonly IRowMapper<(long Id, GraphEdge Edge)> Edge = new EdgeMapper();
    public static readonly IRowMapper<NodeAlias> Alias = new AliasMapper();
    public static readonly IRowMapper<DecisionRow> Decision = new DecisionMapper();
    public static readonly IRowMapper<MapOverride> Override = new OverrideMapper();
    public static readonly IRowMapper<ScopeProposal> Run = new RunMapper();
    public static readonly IRowMapper<MapHead> Head = new HeadMapper();
    public static readonly IRowMapper<string> Since = new TextMapper();
    public static readonly IRowMapper<(int Version, string Since)> Hold = new HoldMapper();

    private static string? Text(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);

    private sealed class IdMapper : IRowMapper<long>
    {
        public long Map(SqliteDataReader reader) => reader.GetInt64(0);
    }

    private sealed class HeadMapper : IRowMapper<MapHead>
    {
        public MapHead Map(SqliteDataReader r) => new(Core.Engine.ContextRepoName.From(r.GetString(0)), r.GetString(1), r.GetInt64(2) == 1);
    }

    private sealed class TextMapper : IRowMapper<string>
    {
        public string Map(SqliteDataReader r) => r.GetString(0);
    }

    private sealed class HoldMapper : IRowMapper<(int, string)>
    {
        public (int, string) Map(SqliteDataReader r) => (r.GetInt32(0), r.GetString(1));
    }

    private sealed class RunMapper : IRowMapper<ScopeProposal>
    {
        public ScopeProposal Map(SqliteDataReader r) => new(r.GetString(0), Core.Engine.ContextRepoName.From(r.GetString(1)), GraphPayload.ReadSurfaces(r.GetString(2)));
    }

    private sealed class VersionMapper : IRowMapper<MapVersion>
    {
        public MapVersion Map(SqliteDataReader r)
        {
            var number = r.GetInt32(0) is > 0 and var id ? id : throw new InvalidOperationException("map_version.id is not positive");
            return new MapVersion(number, Core.Engine.ContextRepoName.From(r.GetString(1)), Text(r, 2), r.GetInt32(3), GraphText.Time(r.GetString(4)) ?? default, GraphText.Status(r.GetString(5)))
            {
                Ruleset = Text(r, 6),
            };
        }
    }

    private sealed class NodeMapper : IRowMapper<GraphNode>
    {
        public GraphNode Map(SqliteDataReader r)
        {
            JsonElement? attributes = null;
            if (Text(r, 2) is { } json)
            {
                using var document = JsonDocument.Parse(json);
                attributes = document.RootElement.Clone();
            }

            return new GraphNode(NodeKey.From(r.GetString(0)), r.GetString(1), GraphText.Provenance(r.GetString(3)), r.GetDouble(4),
                Evidence: null, Attributes: attributes, At: GraphText.Time(Text(r, 5)));
        }
    }

    private sealed class EvidenceMapper : IRowMapper<(string, EvidenceRef)>
    {
        public (string, EvidenceRef) Map(SqliteDataReader r) =>
            (r.IsDBNull(0) ? string.Empty : Convert.ToString(r.GetValue(0), System.Globalization.CultureInfo.InvariantCulture)!,   // why: a non-null column converts to a non-null string
             new EvidenceRef(RecordKey.From(r.GetString(1)), Text(r, 2)));
    }

    private sealed class EdgeMapper : IRowMapper<(long, GraphEdge)>
    {
        public (long, GraphEdge) Map(SqliteDataReader r) => (r.GetInt64(0), new GraphEdge(
            NodeKey.From(r.GetString(1)), NodeKey.From(r.GetString(2)), GraphText.EdgeType(r.GetString(3)), GraphText.Provenance(r.GetString(4)),
            r.GetDouble(5), Evidence: null, Order: r.IsDBNull(6) ? null : r.GetInt32(6), At: GraphText.Time(Text(r, 7))));
    }

    private sealed class AliasMapper : IRowMapper<NodeAlias>
    {
        public NodeAlias Map(SqliteDataReader r) => new(NodeKey.From(r.GetString(0)), NodeKey.From(r.GetString(1)));
    }

    private sealed class DecisionMapper : IRowMapper<DecisionRow>
    {
        public DecisionRow Map(SqliteDataReader r) => new(RecordKey.From(r.GetString(0)), r.GetString(1), r.GetString(2),
            GraphText.Provenance(r.GetString(3)), GraphText.Time(Text(r, 4)));
    }

    private sealed class OverrideMapper : IRowMapper<MapOverride>
    {
        public MapOverride Map(SqliteDataReader r) => new(GraphText.Action(r.GetString(0)), NodeKey.From(r.GetString(1)), r.GetString(2),
            GraphText.Time(r.GetString(3)) ?? default, Text(r, 4), Text(r, 5) is { } other ? NodeKey.From(other) : null, Text(r, 6),
            Text(r, 7) is { } link ? LinkId.From(link) : null, Text(r, 8) is { } undoes ? OverrideId.From(undoes) : null, GraphPayload.Read(Text(r, 9)));
    }
}
