using Aw.Context.Core;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Kernel;

namespace Aw.Context.Features.QueryContext;

internal sealed partial class QueryPipeline
{
    private static Result<(GraphPart Graph, IReadOnlyList<string> Terms)> Symbolic(Result<VisibleMap> map, string symbol, CascadeReach reach, MapText text)
    {
        var implemented = map.Bind(m => FileFeatures(m.Graph, symbol));
        return implemented.IsError
            ? implemented.Error
            : (GraphAnswer.Build(map.Value, implemented.Value, [], reach, text), [.. implemented.Value.SelectMany(a => text.SearchTerms(a.Label)).Distinct(StringComparer.Ordinal)]);
    }

    /// <summary>The nodes a file implements (the symbol shape's file-path form): SND-CONTEXT-003 when the map has no such file or it implements nothing.</summary>
    private static Result<IReadOnlyList<GraphNode>> FileFeatures(GraphSnapshot graph, string symbol)
    {
        var path = symbol.Trim();
        path = RepoPaths.Normalize(path.StartsWith("code:", StringComparison.Ordinal) ? path["code:".Length..] : path);
        var file = NodeKey.TryFrom("code:" + path, out var key) ? graph.Resolve(key) : null;
        IReadOnlyList<GraphNode> features = file is null
            ? []
            : [.. graph.Edges.Where(e => e.Type is EdgeType.Implements or EdgeType.Tags && e.From.Value == file.Key.Value).Select(e => graph.Resolve(e.To)).OfType<GraphNode>()
                .DistinctBy(n => n.Key.Value, StringComparer.Ordinal).OrderBy(n => n.Key.Value, StringComparer.Ordinal)];
        return features.Count > 0
            ? Results.Success(features)
            : ContextErrors.UnknownAnchor.With("no file with that path implements anything in this map (fix: sindri context filemap)");
    }
}
