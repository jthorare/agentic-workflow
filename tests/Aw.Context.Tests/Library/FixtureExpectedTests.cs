using System.Security.Cryptography;
using System.Text.Json;
using Aw.Context.Core.Library;

namespace Aw.Context.Tests.Library;

/// <summary>
/// The parser over <c language="text">tests/fixtures/library</c>, one row per entry of its <c language="text">expected.json</c> (PR review page spec 2026-11-04 §12 Ph0): the codes, the items in document order,
/// the parents, the front-matter links and, where the entry lists them, the refs after the scope step. An entry with no <c language="text">refs</c> key asserts none.
/// </summary>
public sealed class FixtureExpectedTests
{
    public static TheoryData<string> Entries()
    {
        var rows = new TheoryData<string>();
        foreach (var entry in Fixture.Entries.Where(static e => e.Parsed))
        {
            rows.Add(entry.Path);
        }

        return rows;
    }

    [Theory, MemberData(nameof(Entries)), Trait("Lane", "unit")]
    public void The_parser_agrees_with_the_expected_entry(string path)
    {
        var entry = Fixture.Entries.Single(e => e.Path == path);
        var doc = Fixture.Docs[path];

        Assert.Equal(entry.Errors, Fixture.CodesOf(path), StringComparer.Ordinal);
        var wanted = entry.KeepsItems ? entry.Items : [];
        Assert.Equal(wanted, [.. doc.Items.Select(static i => i.Key)], StringComparer.Ordinal);
        foreach (var (item, parent) in entry.Parents)
        {
            Assert.Equal(parent, doc.Items.Single(i => i.Key == item).Parent);
        }

        Assert.Equal(entry.Parent, entry.Parent is null ? null : doc.FrontMatter?.Parent);
        Assert.Equal(entry.Supersedes, entry.Supersedes is null ? null : doc.FrontMatter?.Supersedes);
        if (entry.Refs is { } expected)
        {
            Assert.Equal(expected, [.. Fixture.ScopedRefs(entry)]);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void A_generated_file_whose_source_changed_is_stale_and_an_aw_doc_of_2_is_a_bad_key_not_a_near_miss()
    {
        Assert.Contains(DesignDocCodes.StaleGenerated, Fixture.CodesOf("broken/017-edited-generated/doc.md"), StringComparer.Ordinal);
        Assert.Equal([DesignDocCodes.BadFrontMatter], Fixture.CodesOf("broken/010-aw-doc-2.md"));
        Assert.Equal([DesignDocCodes.NearMiss], Fixture.CodesOf("aw/acme-harbor/plans/20261025-100000-near-misses/float.md"));
    }

    [Fact, Trait("Lane", "unit")]
    public void Heading_anchors_repeat_with_a_number_and_keep_the_double_hyphen_of_a_dropped_ampersand()
    {
        var notes = Fixture.Docs["aw/acme-harbor/plans/20261027-090000-anchors/notes.md"];

        Assert.Equal(["notes", "notes-1", "2-goals--non-goals-v2"], [.. notes.Items.Select(static i => i.Key)], StringComparer.Ordinal);
    }

    private sealed record Entry(string Path, string Scope, bool Parsed, bool KeepsItems, string[] Errors, string[] Items, Dictionary<string, string> Parents, string? Parent, string? Supersedes, string? Source, List<ExpectedRef>? Refs);

    private sealed record ExpectedRef(string? From, string To, string Kind, int Line);

    private static class Fixture
    {
        private static readonly Lazy<(IReadOnlyList<Entry> Entries, IReadOnlyDictionary<string, ParsedDesignDoc> Docs, IReadOnlyDictionary<string, string[]> ScopeCodes, IReadOnlyDictionary<string, IReadOnlyList<DesignRef>> Refs)> _loaded = new(Load);

        internal static IReadOnlyList<Entry> Entries => _loaded.Value.Entries;

        internal static IReadOnlyDictionary<string, ParsedDesignDoc> Docs => _loaded.Value.Docs;

        // The codes the parser gives, then the ones decided against the scope (012, 015, 016), in code order, one per error (a repeat is kept).
        internal static string[] CodesOf(string path) =>
            [.. Docs[path].Errors.Select(static e => e.Code).Concat(_loaded.Value.ScopeCodes.GetValueOrDefault(path, [])).Order(StringComparer.Ordinal)];

        // The refs after the scope step, as the product code reads them (DesignDocScope).
        internal static IEnumerable<ExpectedRef> ScopedRefs(Entry entry) =>
            (_loaded.Value.Refs.TryGetValue(entry.Path, out var refs) ? refs : Docs[entry.Path].Refs).Select(static r => new ExpectedRef(r.From, r.To, Kind(r.Kind), r.Line));

        private static string Kind(DesignRefKind kind) => kind switch
        {
            DesignRefKind.Verifies => "verifies",
            DesignRefKind.Blocks => "blocks",
            DesignRefKind.Relates => "relates",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        private static (IReadOnlyList<Entry>, IReadOnlyDictionary<string, ParsedDesignDoc>, IReadOnlyDictionary<string, string[]>, IReadOnlyDictionary<string, IReadOnlyList<DesignRef>>) Load()
        {
            var root = Path.Combine(RepoRoot(), "tests", "fixtures", "library");
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "expected.json")));
            var entries = json.RootElement.GetProperty("files").EnumerateArray().Select(Read).Where(static e => e.Parsed).ToList();
            var docs = entries.ToDictionary(static e => e.Path, e => DesignDocParser.Parse(e.Path, File.ReadAllText(Path.Combine(root, e.Path)), e.Source is null ? null : Sha256(Path.Combine(root, e.Source))), StringComparer.Ordinal);
            var scoped = entries.Select(e => new ScopedDesignDoc(e.Path, e.Scope, docs[e.Path])).ToList();
            var (findings, refs) = DesignDocScope.Run(scoped);
            var codes = findings.GroupBy(static f => f.Path, StringComparer.Ordinal).ToDictionary(static g => g.Key, static g => g.Select(static f => f.Error.Code).ToArray(), StringComparer.Ordinal);
            return (entries, docs, codes, refs);
        }

        private static Entry Read(JsonElement e)
        {
            var read = Flag(e, "read");
            var noFrontMatter = Str(e, "skipReason") == "no-front-matter";
            var links = e.TryGetProperty("frontMatterLinks", out var l) ? l : e;
            return new Entry(
                Str(e, "path")!,
                Str(e, "scope")!,
                read || Flag(e, "nearMiss") || noFrontMatter,
                Flag(e, "keepsItems"),
                Strings(e, "errors"),
                Strings(e, "items"),
                e.TryGetProperty("parents", out var parents) ? parents.EnumerateObject().ToDictionary(static p => p.Name, static p => p.Value.GetString()!, StringComparer.Ordinal) : new Dictionary<string, string>(StringComparer.Ordinal),
                Str(links, "parent"),
                Str(links, "supersedes"),
                Str(e, "source"),
                e.TryGetProperty("refs", out var refs) ? [.. refs.EnumerateArray().Select(Ref)] : null);
        }

        private static ExpectedRef Ref(JsonElement r) => new(Str(r, "from"), Str(r, "to")!, Str(r, "kind")!, r.GetProperty("line").GetInt32());

        private static string? Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        private static bool Flag(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

        private static string[] Strings(JsonElement e, string name) => e.TryGetProperty(name, out var v) ? [.. v.EnumerateArray().Select(static x => x.GetString()!)] : [];

        private static string Sha256(string file) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file)));

        private static string RepoRoot()
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "AgenticWorkflow.slnx")))
                {
                    return directory.FullName;
                }
            }

            throw new InvalidOperationException("AgenticWorkflow.slnx not found above " + AppContext.BaseDirectory);
        }
    }
}
