using System.Globalization;
using System.Text;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Infrastructure.Graph;
using Aw.CodeIndex.Testing;
using Aw.Kernel;
using Aw.Kernel.Testing;

namespace Aw.CodeIndex.Tests.Graph;

/// <summary>The graph layer's node and edge caps and graphify's two copy caps are gone, the byte cap and the time limit are raised (owner q93, 2026-10-23: CIX-L37 to L39, L43 to L45).</summary>
public sealed class GraphCapsRemovedTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aw-graph-caps-" + RealTime.NewGuid().ToString("N"));

    public GraphCapsRemovedTests()
    {
        _ = Directory.CreateDirectory(_root);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact, Trait("Lane", "unit")]
    public void A_graph_with_more_nodes_and_edges_than_the_old_caps_is_read()
    {
        static string Node(string id, string file) => $$"""{"id":"{{id}}","label":"{{id}}()","source_file":"{{file}}","source_location":"L1","_callable":true}""";
        var nodes = string.Join(',', Enumerable.Range(0, 5).Select(i => Node(string.Create(CultureInfo.InvariantCulture, $"n{i}"), "src/a.ts")));
        var links = string.Join(',', Enumerable.Range(0, 30).Select(i => string.Create(CultureInfo.InvariantCulture, $$"""{"source":"n{{i % 5}}","target":"n{{(i + 1) % 5}}","relation":"calls","confidence":"EXTRACTED","source_file":"src/a.ts","source_location":"L{{i + 1}}"}""")));
        var json = "{\"nodes\":[" + nodes + "],\"links\":[" + links + "]}";

        var parsed = GraphJsonParser.Parse(Encoding.UTF8.GetBytes(json), new HashSet<string>(["src/a.ts"], StringComparer.Ordinal), ["/snap"], new FakeBodyScrubber().Scrub);

        Assert.False(parsed.IsError);
        Assert.Equal(5, parsed.Value.Nodes.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_snapshot_copy_has_no_file_or_total_cap_of_its_own()
    {
        var options = new GraphifyOptions("0.9.67", "/x/graphify", _root);
        var files = new List<SnapshotFile> { new("big.ts", "b-big", 5L * 1024 * 1024) };
        files.AddRange(Enumerable.Range(0, 140).Select(i => new SnapshotFile(string.Create(CultureInfo.InvariantCulture, $"f{i}.ts"), "b", 4L * 1024 * 1024)));
        var request = new GraphRequest(RepoName.From("demo"), CommitSha.From(new string('1', 40)), files, new Blobs(), CodeIndexSettings.Default.Graph);
        var snapshot = Directory.CreateDirectory(Path.Combine(_root, "snapshot")).FullName;

        var written = await GraphSnapshotWriter.Write(request, snapshot, TestContext.Current.CancellationToken);

        Assert.False(written.IsError);
        Assert.Equal(141, Directory.GetFiles(snapshot).Length);
    }

    [Fact, Trait("Lane", "unit")]
    public void Graphify_gets_two_hours_and_a_graph_may_be_a_gigabyte()
    {
        Assert.Equal(TimeSpan.FromHours(2), new GraphifyOptions("0.9.67", "/x/graphify", _root).Timeout);
        Assert.Equal(1_024, CodeIndexSettings.Default.Graph.MaxMegabytes);
    }

    private sealed class Blobs : IBlobReader
    {
        public ValueTask<Result<byte[]?>> Read(string blob, CancellationToken cancellationToken) => ValueTask.FromResult((Result<byte[]?>)new byte[] { 1, 2, 3 });

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
