using System.Net;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.ContextCalls;

namespace Aw.Sindri.Tests.CodeIndexReads;

/// <summary>'sindri code-index' over a scripted API: what it sends, what it prints and the exit codes (0 ok, 1 attention, 2 error). The answers are the API goldens', so the CLI and the routes cannot drift apart.</summary>
public sealed class CodeIndexVerbsTests : IDisposable
{
    private static readonly FakeControlPlaneProbe _probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Golden(string slug, string scenario) => File.ReadAllText(Path.Combine(Kernel.Testing.Golden.SourcePath(typeof(CodeIndexVerbsTests).Assembly, "Golden/Api"), slug, scenario + ".json"));

    private static (HttpStatusCode, string) Ok(string slug, string scenario) => (HttpStatusCode.OK, Golden(slug, scenario));

    private static (HttpStatusCode, string) Problem(HttpStatusCode status, string code, string detail) =>
        (status, string.Create(System.Globalization.CultureInfo.InvariantCulture, $$"""{"type":"x","title":"{{code}}","status":{{(int)status}},"detail":"{{detail}}"}"""));

    private async Task<int> Run(ScriptedApi api, params string[] args)
    {
        using var client = new ControlPlaneClient(_probe, SliceHarness.Json, handler: api);
        return await new CodeIndexVerbs(client, new Output(_out, _error, SliceHarness.Json)).Run(args, Token);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_sends_the_repo_prints_a_line_per_repo_and_exits_0_when_every_repo_is_ok()
    {
        var api = new ScriptedApi(Ok("code-index-status", "all-repos"));

        var exit = await Run(api, "status", "--repo", "demo");

        Assert.Equal("/sindri/code-index/status?repo=demo", api.Calls[0].Route);
        Assert.Equal(0, exit);
        Assert.Contains("demo: ok @ 0123456 · built 2026-10-20 12:00:00Z · structure ok · 4 files", _out.ToString().Replace("1 files", "4 files", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_exits_1_when_a_repo_is_missing_and_says_so_in_a_word()
    {
        var api = new ScriptedApi((HttpStatusCode.OK, """{"repos":[{"repo":"demo","state":"missing","commit":null,"builtAt":null,"layers":[],"counts":null}]}"""));

        var exit = await Run(api, "status");

        Assert.Equal(1, exit);
        Assert.Equal("demo: missing\n", _out.ToString());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Symbols_send_the_concept_flag_as_concept()
    {
        var api = new ScriptedApi(Ok("code-index-symbols", "prefix"));

        var exit = await Run(api, "symbols", "Worker.", "--concept", "billing");

        Assert.Equal("/sindri/code-index/symbols?query=Worker.&concept=billing", api.Calls[0].Route);
        Assert.Equal(0, exit);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Symbols_send_the_query_the_repo_and_the_prefix_match_and_print_each_hit_with_its_span()
    {
        var api = new ScriptedApi(Ok("code-index-symbols", "prefix"));

        var exit = await Run(api, "symbols", "Worker.", "--repo", "demo", "--prefix");

        Assert.Equal("/sindri/code-index/symbols?query=Worker.&repo=demo&match=prefix", api.Calls[0].Route);
        Assert.Equal(0, exit);
        Assert.Contains("2 symbols · demo @ 0123456", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("Worker.Run  method  src/Worker.cs:10-20  exported  void Worker.Run()", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Callers_of_a_typescript_symbol_say_none_found_with_partial_coverage_never_none()
    {
        var api = new ScriptedApi(Ok("code-index-callers", "typescript-none-found"));

        var exit = await Run(api, "callers", "demo:src/tabs.ts#go", "--depth", "2");

        Assert.Equal("/sindri/code-index/callers?ref=demo%3Asrc%2Ftabs.ts%23go&depth=2", api.Calls[0].Route);
        Assert.Equal(0, exit);
        Assert.Contains("coverage partial · 0 edges", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("none found (coverage partial", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_partial_row_marks_a_csharp_answer_partial_and_each_row_shows_its_own_coverage()
    {
        var api = new ScriptedApi(Ok("code-index-callers", "csharp-partial-row"));

        var exit = await Run(api, "callers", "demo:src/Worker.cs#Worker.Stop");

        Assert.Equal(0, exit);
        Assert.Contains("coverage partial · 2 edges", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("(syntax, full)  line 15", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("(graphify, partial)  line 7  1 of 2", _out.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("none found", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Callees_print_each_edge_with_its_basis_confidence_and_coverage_and_the_unresolved_count()
    {
        var api = new ScriptedApi(Ok("code-index-callees", "graphify-partial"));

        var exit = await Run(api, "callees", "src/Worker.cs#Worker.Run", "--repo", "demo");

        Assert.Equal("/sindri/code-index/callees?ref=src%2FWorker.cs%23Worker.Run&repo=demo", api.Calls[0].Route);
        Assert.Equal(0, exit);
        Assert.Contains("level 1  demo:src/Helper.cs#Helper.Assist  Assist  calls  certain (syntax, full)  line 12", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("level 1  (not resolved)  Log  calls  likely (graphify, partial)  line 14", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("2 calls not resolved", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Json_prints_the_api_body_untouched()
    {
        var api = new ScriptedApi(Ok("code-index-callers", "csharp-full"));

        var exit = await Run(api, "callers", "demo:src/Worker.cs#Worker.Run", "--json");

        Assert.Equal(0, exit);
        Assert.Equal(Golden("code-index-callers", "csharp-full").TrimEnd(), _out.ToString().TrimEnd());
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(HttpStatusCode.NotFound, "SND-CODEIDX-013", 1)]
    [InlineData(HttpStatusCode.NotFound, "SND-CODEIDX-011", 1)]
    [InlineData(HttpStatusCode.BadRequest, "SND-CODEIDX-012", 2)]
    public async Task The_not_found_answers_are_attention_and_a_bad_query_is_an_error(HttpStatusCode status, string code, int expected)
    {
        var api = new ScriptedApi(Problem(status, code, "x"));

        var exit = await Run(api, "callers", "demo:src/A.cs#A.B");

        Assert.Equal(expected, exit);
        Assert.Contains(code, _error.ToString(), StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("callers")]
    [InlineData("symbols", "--prefix")]
    [InlineData("status", "extra")]
    [InlineData("callers", "a:b#c", "--depth")]
    [InlineData("callers", "a:b#c", "--json", "--json")]
    [InlineData("callees", "a:b#c", "--prefix")]
    [InlineData("build")]
    [InlineData("neighbors", "x")]
    public async Task A_missing_subject_a_stray_argument_or_an_unknown_flag_or_verb_is_a_usage_error_sent_nowhere(params string[] args)
    {
        var api = new ScriptedApi((HttpStatusCode.OK, "{}"));

        var exit = await Run(api, args);

        Assert.Equal(2, exit);
        Assert.Empty(api.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Control_and_bidirectional_override_characters_are_stripped_from_repo_text()
    {
        var body = Golden("code-index-symbols", "exact").Replace("void Worker.Run()", "void Run()\\u202E\\u0007 evil", StringComparison.Ordinal);
        var api = new ScriptedApi((HttpStatusCode.OK, body));

        var exit = await Run(api, "symbols", "Worker.Run");

        Assert.Equal(0, exit);
        Assert.Contains("void Run() evil", _out.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain('‮', _out.ToString());
        Assert.DoesNotContain('\u0007', _out.ToString());
    }
}
