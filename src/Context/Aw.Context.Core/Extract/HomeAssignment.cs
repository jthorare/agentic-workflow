using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Extract;

/// <summary>What <see cref="HomeFeatures.Assign"/> found: the home and group features with their links, and where each directory and file landed.</summary>
public sealed class HomeAssignment
{
    private readonly Dictionary<string, NodeKey> _requested;

    internal HomeAssignment(ExtractedMap map, IReadOnlyDictionary<string, NodeKey> directories, IReadOnlyDictionary<string, NodeKey> files, IReadOnlyList<FileCluster> requests, Dictionary<string, NodeKey> requested)
    {
        Map = map;
        Directories = directories;
        Files = files;
        LabelRequests = requests;
        _requested = requested;
    }

    /// <summary>The features, the code nodes, the deterministic 'implements' edge of each file and the 'part-of' edge of each child group.</summary>
    public ExtractedMap Map { get; }

    /// <summary>The feature keyed at each directory (a leaf's or a group's, never a '_' leaf), by repo-relative directory path ('' is the repo root).</summary>
    public IReadOnlyDictionary<string, NodeKey> Directories { get; }

    /// <summary>The home feature of each homed file, by repo-relative path.</summary>
    public IReadOnlyDictionary<string, NodeKey> Files { get; }

    /// <summary>What the labeler is asked to name: one entry per feature that holds files, with the paths below it.</summary>
    public IReadOnlyList<FileCluster> LabelRequests { get; }

    /// <summary>The same assignment with the model's names on the features it named.</summary>
    /// <param name="labels">The labels the labeling pass gave, by the ids of <see cref="LabelRequests"/>.</param>
    /// <returns>The assignment; a feature the model did not name keeps its path-derived name.</returns>
    public HomeAssignment WithLabels(IReadOnlyList<ClusterLabel> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var label in labels)
        {
            if (_requested.TryGetValue(label.Id.Value, out var key) && !string.IsNullOrWhiteSpace(label.Label))
            {
                names[key.Value] = string.Join(' ', label.Label.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            }
        }

        var nodes = Map.Nodes.Select(n => names.TryGetValue(n.Key.Value, out var name) ? n with { Label = name } : n).ToList();
        return new HomeAssignment(new ExtractedMap(nodes, Map.Edges), Directories, Files, LabelRequests, _requested);
    }
}
