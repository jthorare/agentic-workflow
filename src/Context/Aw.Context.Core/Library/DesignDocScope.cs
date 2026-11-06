namespace Aw.Context.Core.Library;

/// <summary>
/// The errors decided against a scope's other docs, not in one doc alone: a shared doc id (012), a ref to an item the scope lacks (015), an unknown
/// parent or supersedes (016). Pure; a doc keeps its items whatever it finds here.
/// </summary>
public static class DesignDocScope
{
    /// <summary>Checks a set of parsed docs.</summary>
    /// <param name="docs">The docs, each with its scope; docs in different scopes never share ids.</param>
    /// <returns>The findings, by path then line.</returns>
    public static IReadOnlyList<DesignDocFinding> Check(IReadOnlyList<ScopedDesignDoc> docs)
    {
        // why: the Where on the next line keeps only docs whose front matter and slug exist
        var entries = docs
            .Where(static d => d.Doc.FrontMatter?.Slug is not null && !d.Doc.Errors.Any(static e => string.Equals(e.Code, DesignDocCodes.BadFrontMatter, StringComparison.Ordinal)))
            .Select(static d => new ScopeEntry(d, d.Doc.FrontMatter!.Slug!, d.Doc.FrontMatter))
            .ToList();
        var findings = new List<DesignDocFinding>();
        var scopes = Index(entries, findings);
        foreach (var entry in entries)
        {
            CheckRefs(entry, scopes, findings);
            CheckLink(entry, "parent", entry.Fm.Parent, scopes, findings);
            CheckLink(entry, "supersedes", entry.Fm.Supersedes, scopes, findings);
        }

        return [.. findings.OrderBy(static f => f.Path, StringComparer.Ordinal).ThenBy(static f => f.Error.Line ?? 0).ThenBy(static f => f.Error.Code, StringComparer.Ordinal)];
    }

    private static Dictionary<string, ScopeIndex> Index(List<ScopeEntry> entries, List<DesignDocFinding> findings)
    {
        var scopes = new Dictionary<string, ScopeIndex>(StringComparer.Ordinal);
        foreach (var byScope in entries.GroupBy(static e => e.Doc.Scope, StringComparer.Ordinal))
        {
            var index = new ScopeIndex();
            foreach (var byId in byScope.GroupBy(static e => e.DocId, StringComparer.Ordinal))
            {
                var ordered = byId.OrderBy(static e => e.Doc.Path, StringComparer.Ordinal).ToList();
                index.Winners[byId.Key] = ordered[0].Doc;
                index.Items.UnionWith(ordered[0].Doc.Doc.Items.Select(static i => i.Key));
                findings.AddRange(ordered.Skip(1).Select(loser => new DesignDocFinding(loser.Doc.Path, FrontMatterBlock.Error(
                    loser.Doc.Path,
                    loser.Fm.KeyLines["id"],
                    DesignDocCodes.SharedDocId,
                    $"doc id \"{byId.Key}\" is also used by {ordered[0].Doc.Path}.",
                    "Rename one of them."))));
            }

            scopes[byScope.Key] = index;
        }

        return scopes;
    }

    private static void CheckRefs(ScopeEntry entry, Dictionary<string, ScopeIndex> scopes, List<DesignDocFinding> findings)
    {
        var doc = entry.Doc;
        var own = doc.Doc.Items.Select(static i => i.Key).ToHashSet(StringComparer.Ordinal);
        var foreign = IsForeign(entry.Fm.Parent, doc.Scope) || IsForeign(entry.Fm.Supersedes, doc.Scope);
        foreach (var r in doc.Doc.Refs.DistinctBy(static r => (r.To, r.Line)))
        {
            var ok = r.To.Contains('#', StringComparison.Ordinal) ? Qualified(r.To, doc.Scope, scopes) : own.Contains(r.To) || scopes[doc.Scope].Items.Contains(r.To) || foreign;
            if (!ok)
            {
                findings.Add(new DesignDocFinding(doc.Path, FrontMatterBlock.Error(
                    doc.Path,
                    r.Line,
                    DesignDocCodes.BrokenRef,
                    $"{r.To} is not in this scope.",
                    $"Fix the ref, or add {r.To[(r.To.LastIndexOf('#') + 1)..]} to the doc it names.")));
            }
        }
    }

    private static void CheckLink(ScopeEntry entry, string key, string? value, Dictionary<string, ScopeIndex> scopes, List<DesignDocFinding> findings)
    {
        if (value is null)
        {
            return;
        }

        var (scope, id) = value.StartsWith("doc:", StringComparison.Ordinal) ? SplitDoc(value[4..]) : (entry.Doc.Scope, value);
        if (!scopes.TryGetValue(scope, out var index) || index.Winners.ContainsKey(id))
        {
            return;
        }

        findings.Add(new DesignDocFinding(entry.Doc.Path, FrontMatterBlock.Error(
            entry.Doc.Path,
            entry.Fm.KeyLines[key],
            DesignDocCodes.UnknownDocLink,
            $"{key} \"{value}\" is not a doc in this scope.",
            "Use the id of a doc in the scope, or the full doc:<root>[/<slug>]/<doc id> ref.")));
    }

    private static bool Qualified(string to, string ownScope, Dictionary<string, ScopeIndex> scopes)
    {
        var hash = to.LastIndexOf('#');
        var item = to[(hash + 1)..];
        var (scope, id) = to.StartsWith("doc:", StringComparison.Ordinal) ? SplitDoc(to[4..hash]) : (ownScope, to[..hash]);
        return !scopes.TryGetValue(scope, out var index)
            || (index.Winners.TryGetValue(id, out var target) && target.Doc.Items.Any(i => string.Equals(i.Key, item, StringComparison.Ordinal)));
    }

    private static bool IsForeign(string? link, string ownScope) =>
        link is not null && link.StartsWith("doc:", StringComparison.Ordinal) && !string.Equals(SplitDoc(link[4..]).Scope, ownScope, StringComparison.Ordinal);

    private static (string Scope, string Id) SplitDoc(string target)
    {
        var slash = target.LastIndexOf('/');
        return slash < 0 ? (string.Empty, target) : (target[..slash], target[(slash + 1)..]);
    }

    private sealed record ScopeEntry(ScopedDesignDoc Doc, string DocId, DesignDocFrontMatter Fm);

    private sealed class ScopeIndex
    {
        internal Dictionary<string, ScopedDesignDoc> Winners { get; } = new(StringComparer.Ordinal);

        internal HashSet<string> Items { get; } = new(StringComparer.Ordinal);
    }
}
