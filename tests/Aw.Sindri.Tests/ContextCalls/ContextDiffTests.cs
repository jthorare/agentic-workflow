using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Results;
using Aw.Context.Testing.Fixtures;
using Aw.Context.Testing.Map;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Host.Mcp;
using Aw.Sindri.Host.Text;
using Aw.Sindri.Tests.ApiGoldens;
using ModelContextProtocol.Protocol;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The diff shape on every transport (S-D): GET /sindri/context/query?diff=&amp;repo=, 'sindri context query --diff', and context_query's diff property.</summary>
public sealed class ContextDiffTests : IDisposable
{
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";
    private readonly FixtureRepo _repo = FixtureRepo.Build();

    public void Dispose() => _repo.Dispose();

    private static string Git(FixtureRepo repo, params string[] args)
    {
        var start = new ProcessStartInfo("git") { RedirectStandardOutput = true, UseShellExecute = false };
        start.ArgumentList.Add("-C");
        start.ArgumentList.Add(repo.Path);
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        using var git = Process.Start(start)!;   // why: git is on the test machine's PATH
        var output = git.StandardOutput.ReadToEnd().Trim();
        git.WaitForExit();
        return output;
    }

    private (string Range, MergedPullRequest Pr) Pr3()
    {
        var pr = _repo.PullRequests.Single(p => p.Number == 3);
        return ($"{Git(_repo, "rev-parse", pr.MergeCommit + "^1")}..{Git(_repo, "rev-parse", pr.MergeCommit + "^2")}", pr);
    }

    private async Task<GoldenHost> Mapped(bool sessions, CancellationToken ct)
    {
        var host = await ContextHosts.Approved(_repo, sessions, ct, name: FixtureMap.Repo);
        await ContextMapApiTests.Import(host);
        return host;
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Query_diff_over_http_answers_the_changed_files_with_their_nodes_and_the_unmapped_rest()
    {
        var ct = TestContext.Current.CancellationToken;
        var (range, pr) = Pr3();
        await using var host = await Mapped(sessions: false, ct);

        var answer = await host.Http.Ask(Get($"/sindri/context/query?diff={range}&repo={FixtureMap.Repo}", Human(host.Token.Value)));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        using var body = JsonDocument.Parse(answer.Body);
        var diff = body.RootElement.GetProperty("diff");
        Assert.Equal(pr.Files, diff.GetProperty("files").EnumerateArray().Select(f => f.GetProperty("path").GetString()), StringComparer.Ordinal);
        var split = diff.GetProperty("files").EnumerateArray().Single(f => f.GetProperty("path").GetString() == "src/layout/SplitPane.ts");
        Assert.Equal("mapped", split.GetProperty("status").GetString());
        Assert.Contains("src/commands/registry.ts", diff.GetProperty("unmapped").EnumerateArray().Select(u => u.GetString()), StringComparer.Ordinal);
        Assert.Equal(range, body.RootElement.GetProperty("query").GetProperty("diff").GetString());
        Assert.Equal(["feature:layout.split-pane", "feature:layout.split-pane.orientation"], body.RootElement.GetProperty("anchors").EnumerateArray().Select(a => a.GetString()), StringComparer.Ordinal);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Query_diff_with_a_sha_the_clone_lacks_is_a_110_problem_and_a_branch_name_a_006()
    {
        var ct = TestContext.Current.CancellationToken;
        var (range, _) = Pr3();
        await using var host = await Mapped(sessions: false, ct);

        var unknown = await host.Http.Ask(Get($"/sindri/context/query?diff={new string('1', 40)}..{range.Split("..")[1]}&repo={FixtureMap.Repo}", Human(host.Token.Value)));
        var branch = await host.Http.Ask(Get($"/sindri/context/query?diff=main..{range.Split("..")[1]}&repo={FixtureMap.Repo}", Human(host.Token.Value)));
        var repoOnly = await host.Http.Ask(Get($"/sindri/context/query?q=orientation&repo={FixtureMap.Repo}", Human(host.Token.Value)));

        Assert.Equal((HttpStatusCode.NotFound, "SND-CONTEXT-110"), (unknown.Status, unknown.Code));
        Assert.Equal((HttpStatusCode.BadRequest, "SND-CONTEXT-006"), (branch.Status, branch.Code));
        Assert.Contains("resolve the refs to shas first", branch.Body, StringComparison.Ordinal);
        Assert.Equal((HttpStatusCode.BadRequest, "SND-CONTEXT-006"), (repoOnly.Status, repoOnly.Code));
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Context_query_diff_for_a_session_uses_its_repo()
    {
        var ct = TestContext.Current.CancellationToken;
        var (range, pr) = Pr3();
        await using var host = await Mapped(sessions: true, ct);
        await using var mcp = await McpSession.Open(host, FixtureMap.Repo, ct);

        var tools = await mcp.Client.ListToolsAsync(cancellationToken: ct);
        var result = await mcp.Call("context_query", $$"""{"diff":"{{range}}"}""", ct);

        var properties = tools.Single(t => t.Name == "context_query").JsonSchema.GetProperty("properties");
        Assert.True(properties.TryGetProperty("diff", out _));
        Assert.False(properties.TryGetProperty("repo", out _));   // why: a session's repo is its X-Sindri-Repo
        Assert.NotEqual(true, result.IsError);
        var diff = result.StructuredContent!.Value.GetProperty("diff");
        Assert.Equal(pr.Files, diff.GetProperty("files").EnumerateArray().Select(f => f.GetProperty("path").GetString()), StringComparer.Ordinal);
        var summary = string.Concat(result.Content.OfType<TextContentBlock>().Select(b => b.Text));
        Assert.Contains("diff: 5 changed", summary, StringComparison.Ordinal);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Context_query_diff_for_a_session_with_a_ref_name_is_a_tool_error_naming_006()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await Mapped(sessions: true, ct);
        await using var mcp = await McpSession.Open(host, FixtureMap.Repo, ct);

        var result = await mcp.Call("context_query", """{"diff":"main..HEAD"}""", ct);

        Assert.True(result.IsError);
        Assert.Contains("SND-CONTEXT-006", result.StructuredContent!.Value.GetRawText(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_cli_sends_diff_and_repo_as_query_members_and_checks_the_range_locally()
    {
        var parsed = ContextVerbs.QueryArguments.Parse(["--diff", $"{Sha}..{Sha}", "--repo", "aw-split-panes", "--json"]);
        var branch = ContextVerbs.QueryArguments.Parse(["--diff", "main..HEAD"]);
        var repoOnly = ContextVerbs.QueryArguments.Parse(["--repo", "aw-split-panes"]);

        Assert.Equal($"?diff={Sha}..{Sha}&repo=aw-split-panes", parsed.Value.QueryString);
        Assert.True(parsed.Value.Json);
        Assert.Equal(ContextErrors.QueryInvalid.Code, branch.Error.Code);
        Assert.Contains("resolve the refs to shas first", branch.Error.Message, StringComparison.Ordinal);
        Assert.Equal(ContextErrors.QueryInvalid.Code, repoOnly.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Context_query_repo_without_diff_is_006_exit_1()
    {
        using var error = new StringWriter();
        var output = new Output(TextWriter.Null, error, Kernel.Json.AwJson.CreateOptions(SindriJsonContext.Default));
        var verbs = new ContextVerbs(null!, output);   // why: the argument error returns before the client is touched

        var exit = await verbs.Run(["query", "x", "--repo", "aw-split-panes"], TestContext.Current.CancellationToken);

        Assert.Equal(1, exit);
        Assert.Contains("SND-CONTEXT-006", error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Context_query_diff_text_and_mcp_summary_say_when_the_file_list_is_truncated()
    {
        var sha = "0123456789abcdef0123456789abcdef01234567";
        var part = new DiffPart(ContextRepoName.From("aw-split-panes"), sha, sha, sha, [], [], new DiffCounts(1_500, 1_500, 0, 0, 0), Truncated: true, 45);
        var result = new ContextResult(1, QueryId.From("q_1"), new ContextQuery(Text: null, Diff: new DiffRange(sha, sha)), [], "v1", DateTimeOffset.UnixEpoch,
            [], [], [], [], new Dictionary<string, Citation>(StringComparer.Ordinal), part);

        var text = ContextText.Render(result);

        Assert.Contains("file list truncated to the first 0", text, StringComparison.Ordinal);
        Assert.DoesNotContain("anchors: the first", text, StringComparison.Ordinal);
        Assert.Contains("(file list truncated)", McpSummaries.Query(result), StringComparison.Ordinal);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Query_with_a_malformed_repo_name_is_a_006_problem_before_the_engine_runs()
    {
        var ct = TestContext.Current.CancellationToken;
        var (range, _) = Pr3();
        await using var host = await Mapped(sessions: false, ct);

        var spaced = await host.Http.Ask(Get($"/sindri/context/query?diff={range}&repo=bad%20name", Human(host.Token.Value)));
        var long_ = await host.Http.Ask(Get($"/sindri/context/query?diff={range}&repo={new string('a', 101)}", Human(host.Token.Value)));

        Assert.Equal((HttpStatusCode.BadRequest, "SND-CONTEXT-006"), (spaced.Status, spaced.Code));
        Assert.Equal((HttpStatusCode.BadRequest, "SND-CONTEXT-006"), (long_.Status, long_.Code));
    }

    [Fact, Trait("Lane", "unit")]
    public void Context_query_diff_text_lists_unmapped_before_sections_in_words_with_control_characters_escaped()
    {
        var file = new DiffFile("src/layout/SplitPane.ts", ChangeKind.Modified, DiffStatus.Mapped, [new DiffNode("feature:layout.split-pane", Confirmed: true)]);
        var odd = new DiffFile("src/odd\u001b[31mname.ts", ChangeKind.Added, DiffStatus.Unmapped, []);
        var sha = "0123456789abcdef0123456789abcdef01234567";
        var part = new DiffPart(ContextRepoName.From("aw-split-panes"), sha, sha, "89abcdef0123456789abcdef0123456789abcdef", [odd, file], [odd.Path], new DiffCounts(3, 1, 1, 1, 0), Truncated: false, 1);
        var item = new ResultItem("feature", "feature:layout.split-pane", "Split pane", Text: null, Score: null, ["anchor"], Provenance.Human, Confirmed: true, Trust.Trusted, ["C1"]);
        var result = new ContextResult(1, QueryId.From("q_1"), new ContextQuery(Text: null, Diff: new DiffRange(sha, sha)), ["feature:layout.split-pane"], "v1", DateTimeOffset.UnixEpoch,
            [new ResultSection("graph", [item])], [], [], [], new Dictionary<string, Citation>(StringComparer.Ordinal), part);

        var text = ContextText.Render(result);

        Assert.Contains("diff 0123456..0123456 against merge base 89abcde · 3 changed: 1 mapped, 1 suggested, 1 unmapped, 0 denied", text, StringComparison.Ordinal);
        Assert.Contains("UNMAPPED", text, StringComparison.Ordinal);
        Assert.Contains("src/odd\\x1b[31mname.ts", text, StringComparison.Ordinal);
        Assert.DoesNotContain('\u001b', text);
        Assert.True(text.IndexOf("UNMAPPED", StringComparison.Ordinal) < text.IndexOf("## graph", StringComparison.Ordinal));
    }
}
