using System.Text.Json;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ContextCalls;

namespace Aw.Sindri.Tests.Library;

/// <summary>'sindri library validate' (PR review page spec 2026-11-04 §5.5): the design-doc rules over a file or folder, in process, without Sindri running.</summary>
public sealed class LibraryValidateTests : IDisposable
{
    private const string Good = "---\naw-doc: 1\nid: queue-sizing-spec\ntitle: Queue sizing\nkind: spec\nstatus: draft\n---\n# Queue sizing\n\n## REQ-1 Size the queue\n";

    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly string _dir = Directory.CreateTempSubdirectory("aw-library-validate-").FullName;

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
        Directory.Delete(_dir, recursive: true);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string Write(string relative, string text)
    {
        var path = Path.Combine(_dir, relative);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    private async Task<int> Run(ScriptedApi api, params string[] args)
    {
        using var client = new ControlPlaneClient(new FakeControlPlaneProbe(), SliceHarness.Json, handler: api);
        var output = new Output(_out, _error, SliceHarness.Json);
        using var input = new StringReader(string.Empty);
        return await new LibraryVerbs(new IndexVerbs(client, output, new JobFollower(client, output, TimeProvider.System), terminal: false, input), client, output).Run(["validate", .. args], Token, Token);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_folder_of_good_docs_passes_with_exit_0_and_does_not_call_the_api()
    {
        _ = Write("a/queue-sizing-spec.md", Good);
        _ = Write("notes.md", "# not a design doc\n");
        var api = new ScriptedApi();

        var exit = await Run(api, _dir);

        Assert.Equal(0, exit);
        Assert.Empty(api.Calls);
        Assert.Equal("Checked 1 design doc: no findings\n", _out.ToString().ReplaceLineEndings("\n"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_bad_key_a_near_miss_and_a_shared_id_are_findings_with_exit_1_and_json_names_each()
    {
        var bad = Write("bad.md", "---\naw-doc: 1\nid: bad-one\ntitle: Bad\nkind: spec\nstatus: draft2\n---\n# Bad\n");
        var miss = Write("miss.md", "---\naw-doc: \"1\"\nid: miss\n---\n# Miss\n");
        _ = Write("one.md", Good);
        var two = Write("two.md", Good);

        var exit = await Run(new ScriptedApi(), _dir, "--json");

        Assert.Equal(1, exit);
        using var report = JsonDocument.Parse(_out.ToString());
        Assert.False(report.RootElement.GetProperty("passed").GetBoolean());
        var found = report.RootElement.GetProperty("findings").EnumerateArray().Select(static f => (f.GetProperty("rule").GetString(), f.GetProperty("file").GetString())).ToList();
        Assert.Contains(("SND-LIB-010", bad), found);
        Assert.Contains(("SND-LIB-019", miss), found);
        Assert.Contains(("SND-LIB-012", two), found);
        var first = report.RootElement.GetProperty("findings")[0];
        Assert.True(first.TryGetProperty("line", out _) && first.TryGetProperty("message", out _) && first.TryGetProperty("fix", out _));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_single_file_is_checked_and_text_output_leads_with_the_rule()
    {
        var path = Write("bad.md", "---\naw-doc: 1\nid: bad-one\ntitle: Bad\nkind: spec\nstatus: draft2\n---\n# Bad\n");

        var exit = await Run(new ScriptedApi(), path);

        Assert.Equal(1, exit);
        Assert.StartsWith("SND-LIB-010 ", _out.ToString(), StringComparison.Ordinal);
        Assert.EndsWith("Checked 1 design doc: 1 finding\n", _out.ToString().ReplaceLineEndings("\n"), StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("--pr-body")]
    [InlineData("--repo")]
    [InlineData("--docs")]
    public async Task The_flags_that_need_the_stored_library_are_refused_with_exit_2_and_not_yet(string flag)
    {
        var exit = await Run(new ScriptedApi(), _dir, flag);

        Assert.Equal(2, exit);
        Assert.Contains("not yet", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_missing_path_no_path_and_an_unknown_flag_are_usage_errors()
    {
        var missing = await Run(new ScriptedApi(), Path.Combine(_dir, "nope"));
        var none = await Run(new ScriptedApi());
        var unknown = await Run(new ScriptedApi(), _dir, "--fix");

        Assert.Equal([2, 2, 2], [missing, none, unknown]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Dot_folders_are_not_read()
    {
        _ = Write(".obsidian/hidden.md", "---\naw-doc: 1\nid: hidden\nkind: spec\nstatus: bogus\n---\n");

        var exit = await Run(new ScriptedApi(), _dir);

        Assert.Equal(0, exit);
    }
}
