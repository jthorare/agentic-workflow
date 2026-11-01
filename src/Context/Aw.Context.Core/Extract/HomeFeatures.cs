using System.Security.Cryptography;
using System.Text;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Extract;

/// <summary>Gives every product file of a repo exactly one home feature, chosen by directory and never by a model (spec 2026-10-27 §2).</summary>
public static class HomeFeatures
{
    /// <summary>The most files a home feature holds before its directory is split one level.</summary>
    public const int SizeCap = 150;

    private const int MaxKeyLength = 200;
    private const int LabelFiles = SizeCap;
    private static readonly string[] _outputDirectories = ["node_modules", "dist", "build", "coverage", ".git", "bin", "obj"];

    /// <summary>Assigns the home features of a repo's files.</summary>
    /// <param name="repo">The repo: its name joins the keys, its deny, demote and role globs say which files get no home.</param>
    /// <param name="paths">The repo's full path list (before the readable-file filter, so a '.csproj' can mark a module root).</param>
    /// <param name="extracted">The deterministic extraction: a surface is 'part-of' the home feature of its defining file; null claims none.</param>
    /// <param name="pins">Every concept pin of the build, in the order the ids are made in (the ids are unique across it); null takes the repo's own pins.</param>
    /// <returns>The features, their links and the directory-to-feature lookup.</returns>
    public static HomeAssignment Assign(ContextRepo repo, IReadOnlyList<string> paths, ExtractedMap? extracted = null, IReadOnlyList<ConceptPin>? pins = null)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(paths);
        var all = paths.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var modules = all.Where(IsModuleMarker).Select(DirectoryOf).Where(d => !InOutput(d)).ToHashSet(StringComparer.Ordinal);
        var slots = new List<HomeSlot>();
        foreach (var module in all.Where(p => IsHomed(repo, p)).GroupBy(p => ModuleOf(p, modules), StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var root = new HomeDir(module.Key);
            foreach (var file in module)
            {
                root.Add(module.Key.Length == 0 ? file : file[(module.Key.Length + 1)..], file);
            }

            Plan(root, isModuleRoot: true, parent: null, slots);
        }

        var resolved = PinIds.Resolve(pins ?? repo.Concepts);
        return Build(repo, SplitByPins(slots, PinIds.Of(resolved, repo.Concepts)), extracted);
    }

    /// <summary>Whether a feature key is a home feature of <paramref name="repo"/> (its key starts with the repo's escaped name), as opposed to a feature a recognizer or tag made.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="key">A feature key.</param>
    /// <returns>True for a home feature (the repo's root, a directory, a loose-files leaf or a pin's leaf).</returns>
    internal static bool IsHomeKey(ContextRepoName repo, NodeKey key)
    {
        var prefix = "feature:" + Escape(repo.Value);
        var value = key.Value;
        return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && (value.Length == prefix.Length || value[prefix.Length] is '.' or '~');
    }

    // A pin splits the final leaves (spec 2026-10-27 §10.1): a leaf whose files all belong to one pin, or to none, keeps its plain key; one that spans pins gets a leaf per pin ('~<id>') and the unpinned rest keeps the plain key.
    private static List<HomeSlot> SplitByPins(List<HomeSlot> slots, IReadOnlyList<ResolvedPin> pins)
    {
        if (pins.Count == 0)
        {
            return slots;
        }

        var result = new List<HomeSlot>(slots.Count);
        var order = pins.Select(p => p.Id).ToList();
        foreach (var slot in slots)
        {
            if (slot.Group || slot.Files.Count == 0)
            {
                result.Add(slot);
                continue;
            }

            var byPin = slot.Files.GroupBy(f => pins.FirstOrDefault(p => PathDenylist.MatchesExactCase(f, p.Pin.Paths))?.Id, StringComparer.Ordinal).ToList();
            if (byPin.Count == 1)
            {
                slot.PinId = byPin[0].Key;
                result.Add(slot);
                continue;
            }

            foreach (var part in byPin.OrderBy(g => g.Key is null ? -1 : order.IndexOf(g.Key)))
            {
                result.Add(new HomeSlot(slot.Path, slot.Loose, group: false, [.. part], slot.Parent) { PinId = part.Key, Suffixed = part.Key is not null });
            }
        }

        return result;
    }

    private static void Plan(HomeDir node, bool isModuleRoot, HomeSlot? parent, List<HomeSlot> slots)
    {
        if (node.Total == 0)
        {
            return;
        }

        if (node.Total <= SizeCap || node.Children.Count == 0)
        {
            slots.Add(new HomeSlot(node.Path, loose: false, group: false, node.AllFiles(), parent));
            return;
        }

        var group = new HomeSlot(node.Path, loose: false, group: true, [], parent);
        slots.Add(group);
        if (isModuleRoot && node.Children.TryGetValue("src", out var src) && node.Total - src.Total <= SizeCap)
        {
            var rest = node.Loose.Concat(node.Children.Where(c => c.Key != "src").SelectMany(c => c.Value.AllFiles())).Order(StringComparer.Ordinal).ToList();
            if (rest.Count > 0)
            {
                slots.Add(new HomeSlot(node.Path, loose: true, group: false, rest, group));
            }

            Plan(src, isModuleRoot: false, group, slots);
            return;
        }

        if (node.Loose.Count > 0)
        {
            slots.Add(new HomeSlot(node.Path, loose: true, group: false, [.. node.Loose.Order(StringComparer.Ordinal)], group));
        }

        foreach (var child in node.Children.OrderBy(c => c.Key, StringComparer.Ordinal))
        {
            Plan(child.Value, isModuleRoot: false, group, slots);
        }
    }

    private static HomeAssignment Build(ContextRepo repo, List<HomeSlot> slots, ExtractedMap? extracted)
    {
        var keys = Keys(repo.Name, slots);
        var nodes = new List<GraphNode>();
        var edges = new List<GraphEdge>();
        var files = new Dictionary<string, NodeKey>(StringComparer.Ordinal);
        var directories = new Dictionary<string, NodeKey>(StringComparer.Ordinal);
        var requests = new List<FileCluster>();
        var requested = new Dictionary<string, NodeKey>(StringComparer.Ordinal);
        foreach (var slot in slots)
        {
            var key = keys[slot];
            nodes.Add(new GraphNode(key, LabelOf(repo.Name, slot), Provenance.Deterministic));
            Link(slot, key, keys, directories, edges);

            if (slot.Files.Count == 0)
            {
                continue;
            }

            var id = ClusterId.From("h" + requests.Count.ToString("D5", System.Globalization.CultureInfo.InvariantCulture));
            requests.Add(new FileCluster(id, [.. slot.Files.Take(LabelFiles)], 1.0) { FileName = LabelOf(repo.Name, slot) });
            requested[id.Value] = key;
            foreach (var file in slot.Files)
            {
                files[file] = key;
                nodes.Add(new GraphNode(NodeKey.From("code:" + file), file[(file.LastIndexOf('/') + 1)..], Provenance.Deterministic));
                edges.Add(HomeEdge(repo.Name, file, key));
            }
        }

        edges.AddRange(Claims(extracted, files));
        return new HomeAssignment(ExtractedMap.Empty.Plus(new ExtractedMap(nodes, edges)), directories, files, requests, requested);
    }

    // A directory split by pins is named by its unpinned rest, else by its first pin's leaf; a leaf belongs to its pin and is part of its parent group.
    private static void Link(HomeSlot slot, NodeKey key, Dictionary<HomeSlot, NodeKey> keys, Dictionary<string, NodeKey> directories, List<GraphEdge> edges)
    {
        if (!slot.Loose)
        {
            if (slot.Suffixed)
            {
                _ = directories.TryAdd(slot.Path, key);
            }
            else
            {
                directories[slot.Path] = key;
            }
        }

        if (slot.PinId is { } pinId)
        {
            edges.Add(new GraphEdge(key, NodeKey.From("concept:" + pinId), EdgeType.BelongsTo, Provenance.Deterministic));
        }

        if (slot.Parent is { } parent)
        {
            edges.Add(new GraphEdge(key, keys[parent], EdgeType.PartOf, Provenance.Deterministic));
        }
    }

    private static GraphEdge HomeEdge(ContextRepoName repo, string file, NodeKey home)
    {
        var evidence = RecordKey.TryFrom($"repo:{repo.Value}/{file}", out var record) ? new[] { new EvidenceRef(record) } : null;
        return new GraphEdge(NodeKey.From("code:" + file), home, EdgeType.Implements, Provenance.Deterministic, 1.0, evidence);
    }

    // A surface is part of the home feature of the file that defines it (the first one in path order when two files do).
    private static IEnumerable<GraphEdge> Claims(ExtractedMap? extracted, Dictionary<string, NodeKey> files)
    {
        if (extracted is null)
        {
            yield break;
        }

        var claimed = new HashSet<string>(StringComparer.Ordinal);
        var defining = extracted.Edges.Where(e => e is { Type: EdgeType.Implements, From.Kind: NodeKind.Code, To.Kind: NodeKind.Surface })
            .OrderBy(e => e.From.Value, StringComparer.Ordinal).ThenBy(e => e.To.Value, StringComparer.Ordinal);
        foreach (var edge in defining)
        {
            if (files.TryGetValue(edge.From.Value["code:".Length..], out var home) && claimed.Add(edge.To.Value))
            {
                yield return new GraphEdge(edge.To, home, EdgeType.PartOf, Provenance.Deterministic);
            }
        }
    }

    // Every key comes from the repo name and the path alone. A key two paths share once the key function has lowercased it gets a hash of its path on every holder, so a new sibling never renames an old key.
    private static Dictionary<HomeSlot, NodeKey> Keys(ContextRepoName repo, List<HomeSlot> slots)
    {
        var bases = slots.ToDictionary(s => s, s => Fit(Base(repo, s), s, 8));
        var shared = slots.GroupBy(s => Normal(bases[s]), StringComparer.Ordinal).Where(g => g.Skip(1).Any()).SelectMany(g => g).ToHashSet();
        return slots.ToDictionary(s => s, s => KeyOf(shared.Contains(s) ? Fit(Base(repo, s) + "-" + Hash(s.Identity, 6), s, 8) : bases[s]));
    }

    private static string Normal(string raw) => KeyOf(raw).Value;

    private static NodeKey KeyOf(string raw) => EntryKey.For(NodeKey.From(raw));

    private static string Base(ContextRepoName repo, HomeSlot slot) =>
        "feature:" + Escape(repo.Value) + (slot.Path.Length > 0 ? "." + string.Join('.', slot.Path.Split('/').Select(Escape)) : "") + (slot.Loose ? "._" : "") + slot.KeySuffix;

    // A '_' in a name is written '__' and a '.' '_', so a/b.c, a/b_c and a.b/c differ and no escaped name ends in a lone '_'.
    // A '~' in a name is written '~~', so the pin suffix '~<id>' is never part of a directory name.
    private static string Escape(string name) => name.Replace("_", "__", StringComparison.Ordinal).Replace("~", "~~", StringComparison.Ordinal).Replace('.', '_');

    private static string Fit(string raw, HomeSlot slot, int hex)
    {
        if (raw.Length <= MaxKeyLength)
        {
            return raw;
        }

        var cut = MaxKeyLength - 1 - hex;
        if (char.IsHighSurrogate(raw[cut - 1]))
        {
            cut--;
        }

        return raw[..cut] + "-" + Hash(slot.Identity, hex);
    }

    private static string Hash(string text, int length) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..length];

    private static string LabelOf(ContextRepoName repo, HomeSlot slot)
    {
        var name = slot.Path.Length == 0 ? repo.Value : slot.Path[(slot.Path.LastIndexOf('/') + 1)..];
        var label = ClusterFeatures.Humanize(name);
        label = slot.Loose ? label + " (loose files)" : label;
        return slot.Suffixed ? $"{label} ({slot.PinId})" : label;
    }

    private static bool IsHomed(ContextRepo repo, string path) =>
        Recognizers.MayRead(path)
        && !path.EndsWith(".json", StringComparison.Ordinal)
        && !InOutput(path)
        && !ManifestFiles.Is(path)
        && !repo.Denies(path)
        && !repo.Demotes(path)
        && NodeKey.TryFrom("code:" + path, out _);

    private static bool InOutput(string path) => path.Split('/').Any(s => _outputDirectories.Contains(s, StringComparer.Ordinal));

    private static bool IsModuleMarker(string path)
    {
        var name = path[(path.LastIndexOf('/') + 1)..];
        return string.Equals(name, "package.json", StringComparison.Ordinal) || name.EndsWith(".csproj", StringComparison.Ordinal);
    }

    private static string DirectoryOf(string path) => path.Contains('/', StringComparison.Ordinal) ? path[..path.LastIndexOf('/')] : "";

    private static string ModuleOf(string path, HashSet<string> modules)
    {
        var dir = DirectoryOf(path);
        while (dir.Length > 0 && !modules.Contains(dir))
        {
            dir = DirectoryOf(dir);
        }

        return dir;
    }
}
