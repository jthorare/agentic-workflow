using System.Net;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ContextCalls;

namespace Aw.Sindri.Tests.Library;

/// <summary>'sindri library list' and 'sindri library show' over a scripted API (PR review page spec 2026-11-04 §5.5, §6.1): they read the routes and never start the control plane.</summary>
public sealed class LibraryReadVerbsTests : IDisposable
{
    private const string AsOf = """{"cursor":5,"ledgerHead":5,"projectedAt":"2026-01-01T00:00:00.0000000Z","rebuilding":false,"stalled":false}""";

    private const string Broken =
        """{"asOf":ASOF,"docs":41,"withErrors":1,"broken":[{"doc":"booking-retry-tdd","path":"~/notes/design/booking-retry-tdd.md","errors":[{"rule":"SND-LIB-010","text":"line 4: status 'draft2' isn't allowed. Fix it"},{"rule":"SND-LIB-015","text":"line 31: REQ-9 is not in this scope"}]}],"nearMisses":[{"path":"~/notes/design/retry-notes.md","rule":"SND-LIB-019","text":"line 1: the first line is blank"}],"unreadableRoots":[{"root":"~/notes/vault","reason":"permission denied"}],"cappedText":"Showing the first 200 of 250 docs with errors; run `sindri library validate` for the rest","roots":[]}""";

    private const string Empty = """{"asOf":ASOF,"docs":0,"withErrors":0,"broken":[],"nearMisses":[],"unreadableRoots":[],"cappedText":null,"roots":[]}""";

    private const string Doc =
        """{"asOf":ASOF,"doc":{"id":"booking-retry-tdd","root":"aw","scope":"aw/acme-api","kind":"tdd","status":"draft","project":null,"repos":["acme/api"],"issues":["412"],"title":"Booking retry TDD","path":"~/.agentic-workflow/acme-api/plans/booking-retry-tdd.md","errors":[{"rule":"SND-LIB-015","text":"line 31: REQ-9 is not in this scope"}]}}""";

    private static readonly FakeControlPlaneProbe _up = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static (HttpStatusCode, string) Ok(string body) => (HttpStatusCode.OK, body.Replace("ASOF", AsOf, StringComparison.Ordinal));

    private async Task<int> Run(FakeControlPlaneProbe probe, ScriptedApi api, params string[] args)
    {
        using var client = new ControlPlaneClient(probe, SliceHarness.Json, handler: api);
        var output = new Output(_out, _error, SliceHarness.Json);
        using var input = new StringReader(string.Empty);
        return await new LibraryVerbs(new IndexVerbs(client, output, new JobFollower(client, output, TimeProvider.System), terminal: false, input), client, output).Run(args, Token, Token);
    }

    private string Text => _out.ToString().ReplaceLineEndings("\n");

    [Fact, Trait("Lane", "unit")]
    public async Task List_prints_the_head_each_broken_doc_with_its_rules_the_near_misses_the_unreadable_folders_and_the_cap()
    {
        var api = new ScriptedApi(Ok(Broken));

        var exit = await Run(_up, api, "list");

        Assert.Equal(0, exit);
        Assert.Equal(("GET", "/sindri/library"), (api.Calls[0].Method, api.Calls[0].Route));
        Assert.Equal(
            string.Join(
                '\n',
                "Library: 41 docs, 1 with errors",
                "~/notes/design/booking-retry-tdd.md",
                "  SND-LIB-010 line 4: status 'draft2' isn't allowed. Fix it",
                "  SND-LIB-015 line 31: REQ-9 is not in this scope",
                "~/notes/design/retry-notes.md",
                "  SND-LIB-019 line 1: the first line is blank",
                "~/notes/vault: can't be read (permission denied)",
                "Showing the first 200 of 250 docs with errors; run `sindri library validate` for the rest",
                string.Empty),
            Text);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task List_of_an_empty_library_says_there_are_no_docs_yet()
    {
        var exit = await Run(_up, new ScriptedApi(Ok(Empty)), "list");

        Assert.Equal(0, exit);
        Assert.Equal("Library: no design docs yet\n", Text);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task List_errors_prints_only_the_broken_docs_and_near_misses_only_the_near_misses()
    {
        _ = await Run(_up, new ScriptedApi(Ok(Broken)), "list", "--errors");
        var errors = Text;
        _out.GetStringBuilder().Clear();
        _ = await Run(_up, new ScriptedApi(Ok(Broken)), "list", "--near-misses");

        Assert.DoesNotContain("Library:", errors, StringComparison.Ordinal);
        Assert.Contains("booking-retry-tdd.md", errors, StringComparison.Ordinal);
        Assert.DoesNotContain("retry-notes", errors, StringComparison.Ordinal);
        Assert.Equal("~/notes/design/retry-notes.md\n  SND-LIB-019 line 1: the first line is blank\n", Text);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task List_json_prints_the_routes_body_as_it_came()
    {
        var body = Broken.Replace("ASOF", AsOf, StringComparison.Ordinal);

        var exit = await Run(_up, new ScriptedApi((HttpStatusCode.OK, body)), "list", "--json");

        Assert.Equal(0, exit);
        Assert.Equal(body + "\n", Text);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("--errors", "--near-misses")]
    [InlineData("--all")]
    public async Task List_with_flags_that_do_not_go_together_is_a_usage_error_that_calls_nothing(params string[] flags)
    {
        var api = new ScriptedApi();

        var exit = await Run(_up, api, ["list", .. flags]);

        Assert.Equal(2, exit);
        Assert.Empty(api.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task List_and_show_when_sindri_is_not_running_exit_1_without_starting_it()
    {
        var down = new FakeControlPlaneProbe();
        var api = new ScriptedApi();

        var list = await Run(down, api, "list");
        var show = await Run(down, api, "show", "booking-retry-tdd");

        Assert.Equal((1, 1), (list, show));
        Assert.Contains("SND-API-001", _error.ToString(), StringComparison.Ordinal);
        Assert.Empty(api.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Show_prints_the_doc_its_file_and_its_errors()
    {
        var api = new ScriptedApi(Ok(Doc));

        var exit = await Run(_up, api, "show", "booking-retry-tdd");

        Assert.Equal(0, exit);
        Assert.Equal("/sindri/library/booking-retry-tdd", api.Calls[0].Route);
        Assert.Equal(
            string.Join(
                '\n',
                "booking-retry-tdd  tdd, draft  Booking retry TDD",
                "~/.agentic-workflow/acme-api/plans/booking-retry-tdd.md",
                "root aw · scope aw/acme-api",
                "repos acme/api",
                "issues 412",
                "  SND-LIB-015 line 31: REQ-9 is not in this scope",
                string.Empty),
            Text);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Show_of_an_unknown_doc_prints_the_404_and_exits_2()
    {
        var problem = """{"type":"about:blank","title":"SND-LIB-404","status":404,"detail":"No design doc 'nope'; `sindri library list` names them"}""";

        var exit = await Run(_up, new ScriptedApi((HttpStatusCode.NotFound, problem)), "show", "nope");

        Assert.Equal(2, exit);
        Assert.Contains("SND-LIB-404", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Show_json_prints_the_body_and_show_with_no_doc_is_a_usage_error()
    {
        var body = Doc.Replace("ASOF", AsOf, StringComparison.Ordinal);
        var json = await Run(_up, new ScriptedApi((HttpStatusCode.OK, body)), "show", "booking-retry-tdd", "--json");
        var printed = Text;
        var api = new ScriptedApi();
        var none = await Run(_up, api, "show");

        Assert.Equal((0, 2), (json, none));
        Assert.Equal(body + "\n", printed);
        Assert.Empty(api.Calls);
    }
}
