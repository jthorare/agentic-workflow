using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Scoping;

namespace Aw.Sindri.Tests.Commands;

/// <summary>
/// 'sindri scoping check|show|desk' as Program runs them (scoping spec §8.2): the local --facts mode over a directory of fact files, the
/// refusals inside a git worktree (SND-SCOPING-002), and a serve that is down (SND-API-001, exit 2). It swaps the process's console.
/// </summary>
[Collection(ProcessEnvironment.Name)]
public sealed class ScopingCommandTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly TextWriter _consoleOut = Console.Out;
    private readonly TextWriter _consoleError = Console.Error;

    public ScopingCommandTests()
    {
        Console.SetOut(_out);
        Console.SetError(_error);
    }

    public void Dispose()
    {
        Console.SetOut(_consoleOut);
        Console.SetError(_consoleError);
        _home.Dispose();
    }

    private string Facts => Path.Combine(_home.Path, "facts");

    private async Task Put(ScopingFacts facts, string? directory = null)
    {
        var target = directory ?? Facts;
        _ = Directory.CreateDirectory(target);
        foreach (var (key, text) in facts)
        {
            await File.WriteAllTextAsync(Path.Combine(target, key + ".json"), text, TestContext.Current.CancellationToken);
        }
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Check_on_the_gold_facts_exits_0_and_json_prints_the_findings_shape()
    {
        await Put(ScopingFacts.Expiry());

        Assert.Equal(0, await Run("scoping", "check", "--facts", Facts, "--stage", "draft", "--json"));

        using var body = JsonDocument.Parse(_out.ToString());
        Assert.Equal("draft", body.RootElement.GetProperty("stage").GetString());
        Assert.Equal("pass", body.RootElement.GetProperty("outcome").GetString());
        Assert.Equal(0, body.RootElement.GetProperty("findings").GetArrayLength());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Check_on_a_broken_fixture_exits_1_and_names_the_rule()
    {
        await Put(ScopingFacts.Expiry().Edit("acceptance", n => n["items"] = new JsonArray()));

        Assert.Equal(1, await Run("scoping", "check", "--facts", Facts));

        Assert.Contains("SCP-ACC-001", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("fix:", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Check_defaults_to_the_draft_stage_and_runs_every_rule_at_evidence()
    {
        await Put(ScopingFacts.Expiry().Without("answers"));

        Assert.Equal(0, await Run("scoping", "check", "--facts", Facts));
        _out.GetStringBuilder().Clear();
        Assert.Equal(1, await Run("scoping", "check", "--facts", Facts, "--stage", "evidence"));
        Assert.Contains("SCP-ANS-001", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("not run", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_draft_overlay_replaces_the_fact_files_it_names()
    {
        await Put(ScopingFacts.Expiry());
        var draft = Path.Combine(_home.Path, "run", "draft");
        await Put(new ScopingFacts { ["acceptance"] = """{"items":[]}""" }, draft);

        Assert.Equal(1, await Run("scoping", "check", "--facts", Facts, "--draft", draft));
        Assert.Contains("SCP-ACC-001", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_draft_dir_inside_a_git_worktree_exits_2_with_SND_SCOPING_002()
    {
        await Put(ScopingFacts.Expiry());
        var worktree = Path.Combine(_home.Path, "checkout");
        _ = Directory.CreateDirectory(worktree);
        await File.WriteAllTextAsync(Path.Combine(worktree, ".git"), "gitdir: /elsewhere\n", TestContext.Current.CancellationToken);
        var draft = Path.Combine(worktree, "drafts");
        await Put(new ScopingFacts { ["acceptance"] = """{"items":[]}""" }, draft);

        Assert.Equal(2, await Run("scoping", "check", "--facts", Facts, "--draft", draft));

        Assert.Contains("SND-SCOPING-002", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("(fix: ", _error.ToString(), StringComparison.Ordinal);
        Assert.Empty(_out.ToString());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Show_prints_the_nine_sections_as_text_and_json_matches_the_golden()
    {
        await Put(ScopingShowTests.TwoLimitedLenses());

        Assert.Equal(0, await Run("scoping", "show", "--facts", Facts));
        var text = _out.ToString();
        Assert.Contains("2 of 4 lenses limited", text, StringComparison.Ordinal);
        Assert.Contains("not searched: history-unlinked — Wait for the ticket linker (S-5B)", text, StringComparison.Ordinal);

        _out.GetStringBuilder().Clear();
        Assert.Equal(0, await Run("scoping", "show", "--facts", Facts, "--json"));
        using var body = JsonDocument.Parse(_out.ToString());
        Assert.Equal(9, body.RootElement.GetProperty("sections").GetArrayLength());
        Assert.StartsWith("2 of 4 lenses limited", body.RootElement.GetProperty("coverageHeader").GetString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Show_fences_the_untrusted_text_for_a_session()
    {
        await Put(ScopingFacts.Expiry());
        _session = true;   // AW_SINDRI_CHILD makes the caller a session whatever process runs the test

        Assert.Equal(0, await Run("scoping", "show", "--facts", Facts));

        var shown = _out.ToString();
        Assert.Contains("<untrusted source=\"scoping\">AC-1 [ears] When a member shares a note and picks an expiry time", shown, StringComparison.Ordinal);   // acceptance text
        Assert.Contains("<untrusted source=\"scoping\">▣ S1 feature:sharing — Public link sharing</untrusted>", shown, StringComparison.Ordinal);   // a map title drawn from a lens
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Desk_check_writes_the_report_and_appends_rounds_to_the_out_dir()
    {
        await Put(ScopingFacts.Expiry().Without("rounds"));
        var outDir = Path.Combine(_home.Path, "run");

        Assert.Equal(0, await Run("scoping", "desk", "check", "--facts", Facts, "--out", outDir));
        Assert.Equal(0, await Run("scoping", "desk", "check", "--facts", Facts, "--out", outDir));

        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(outDir, "report.json"), TestContext.Current.CancellationToken));
        Assert.Equal("pass", report.RootElement.GetProperty("outcome").GetString());
        Assert.Equal("step.scope.check", report.RootElement.GetProperty("block").GetString());
        var rounds = (await File.ReadAllLinesAsync(Path.Combine(outDir, "rounds.jsonl"), TestContext.Current.CancellationToken)).Select(static l => JsonNode.Parse(l)!).ToList();
        Assert.Equal([1, 2], rounds.Select(static r => r["round"]!.GetValue<int>()));
        Assert.Equal(["first", "revise"], rounds.Select(static r => r["trigger"]!.GetValue<string>()), StringComparer.Ordinal);
        Assert.Equal(10, rounds[0]["added"]!.AsArray().Count);   // the first round adds every item
        Assert.Empty(rounds[1]["added"]!.AsArray());   // the second diffs against the first, which only the out directory holds
        Assert.Empty(rounds[1]["changed"]!.AsArray());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Desk_check_reports_the_findings_as_a_fact_file_the_complete_verb_can_take()
    {
        await Put(ScopingFacts.Expiry().Edit("acceptance", n => n["items"] = new JsonArray()));
        var outDir = Path.Combine(_home.Path, "run");

        Assert.Equal(1, await Run("scoping", "desk", "check", "--facts", Facts, "--out", outDir));

        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(outDir, "report.json"), TestContext.Current.CancellationToken));
        Assert.Equal("gaps", report.RootElement.GetProperty("outcome").GetString());
        Assert.Equal("findings.json", report.RootElement.GetProperty("outputs").GetProperty("fact:findings").GetString());
        Assert.True(File.Exists(Path.Combine(outDir, "findings.json")));
        var byOutcome = report.RootElement.GetProperty("outputsByOutcome");   // what a complete on each outcome takes, from the descriptor
        Assert.Equal(["fact:findings"], byOutcome.GetProperty("gaps").EnumerateArray().Select(static e => e.GetString()), StringComparer.Ordinal);
        Assert.Equal(["fact:findings"], byOutcome.GetProperty("regather").EnumerateArray().Select(static e => e.GetString()), StringComparer.Ordinal);
        Assert.Empty(byOutcome.GetProperty("pass").EnumerateArray());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Desk_out_inside_a_git_worktree_exits_2_with_SND_SCOPING_002()
    {
        await Put(ScopingFacts.Expiry());
        var worktree = Path.Combine(_home.Path, "checkout");
        _ = Directory.CreateDirectory(Path.Combine(worktree, ".git"));

        Assert.Equal(2, await Run("scoping", "desk", "check", "--facts", Facts, "--out", Path.Combine(worktree, "out")));

        Assert.Contains("SND-SCOPING-002", _error.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(worktree, "out")));
    }

    private string Out => Path.Combine(_home.Path, "run");

    private async Task<string> Repo(string path, string content)
    {
        var root = Path.Combine(_home.Path, "repo");
        var file = Path.Combine(root, path);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await File.WriteAllTextAsync(file, content, TestContext.Current.CancellationToken);
        return root;
    }

    private Task ReadBriefInput(string path) => Put(new ScopingFacts
    {
        ["scopeInput"] = $$"""{"kind":"brief","repo":"fixture-notes","path":"{{path}}"}""",
        ["contextQuery"] = ScopingFacts.View("contextQuery"),
    });

    [Fact, Trait("Lane", "integration")]
    public async Task Desk_read_brief_reads_the_file_under_the_repo_root_and_writes_the_brief_and_anchors()
    {
        var root = await Repo("briefs/expiry.md", "# Shared links expire\n\nA shared link stays open for ever.\n");
        await ReadBriefInput("briefs/expiry.md");

        Assert.Equal(0, await Run("scoping", "desk", "read-brief", "--facts", Facts, "--out", Out, "--repo-root", root, "--json"));

        var brief = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(Out, "brief.json"), TestContext.Current.CancellationToken))!;
        Assert.Equal("Shared links expire", brief["subject"]!.GetValue<string>());
        Assert.Equal("briefs/expiry.md", brief["refs"]![0]!["key"]!.GetValue<string>());
        Assert.Matches("^sha256:[0-9a-f]{12}$", brief["refs"]![0]!["version"]!.GetValue<string>());
        Assert.True(File.Exists(Path.Combine(Out, "anchors.json")));
        using var report = JsonDocument.Parse(_out.ToString());
        Assert.Equal("read", report.RootElement.GetProperty("outcome").GetString());
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("../outside.md")]
    [InlineData(".env")]
    [InlineData("secrets/key.pem")]
    [InlineData(".git/config")]
    public async Task Desk_read_brief_refuses_a_path_outside_the_roots_or_under_a_denied_glob_with_SND_SCOPING_004(string path)
    {
        var root = await Repo("briefs/expiry.md", "# x\n");
        _ = await Repo(path.Replace("../", string.Empty, StringComparison.Ordinal), "TOKEN=1\n");
        await File.WriteAllTextAsync(Path.Combine(_home.Path, "outside.md"), "# outside\n", TestContext.Current.CancellationToken);
        await ReadBriefInput(path);

        Assert.Equal(2, await Run("scoping", "desk", "read-brief", "--facts", Facts, "--out", Out, "--repo-root", root));

        Assert.Contains("SND-SCOPING-004", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("(fix: ", _error.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(Out, "brief.json")));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Desk_read_brief_follows_no_symlink_out_of_the_root()
    {
        var root = await Repo("briefs/expiry.md", "# x\n");
        await File.WriteAllTextAsync(Path.Combine(_home.Path, "outside.md"), "# outside\n", TestContext.Current.CancellationToken);
        _ = File.CreateSymbolicLink(Path.Combine(root, "briefs", "link.md"), Path.Combine(_home.Path, "outside.md"));
        await ReadBriefInput("briefs/link.md");

        Assert.Equal(2, await Run("scoping", "desk", "read-brief", "--facts", Facts, "--out", Out, "--repo-root", root));

        Assert.Contains("SND-SCOPING-004", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Desk_read_brief_refuses_a_file_reached_through_a_symlinked_directory_that_leaves_the_root()
    {
        var root = await Repo("briefs/expiry.md", "# x\n");
        var outside = Path.Combine(_home.Path, "elsewhere");
        _ = Directory.CreateDirectory(outside);
        await File.WriteAllTextAsync(Path.Combine(outside, "secret.md"), "# outside\n", TestContext.Current.CancellationToken);
        _ = Directory.CreateSymbolicLink(Path.Combine(root, "docs"), outside);
        await ReadBriefInput("docs/secret.md");

        Assert.Equal(2, await Run("scoping", "desk", "read-brief", "--facts", Facts, "--out", Out, "--repo-root", root));

        Assert.Contains("SND-SCOPING-004", _error.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(Out, "brief.json")));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Desk_read_brief_reads_through_a_symlinked_directory_that_stays_inside_the_root()
    {
        var root = await Repo("briefs/expiry.md", "# Inside\n");
        _ = Directory.CreateSymbolicLink(Path.Combine(root, "docs"), Path.Combine(root, "briefs"));
        await ReadBriefInput("docs/expiry.md");

        Assert.Equal(0, await Run("scoping", "desk", "read-brief", "--facts", Facts, "--out", Out, "--repo-root", root));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Desk_gather_takes_the_three_views_as_files_and_writes_lenses_and_coverage()
    {
        await Put(new ScopingFacts { ["anchors"] = ScopingFacts.Expiry()["anchors"] });
        var views = Path.Combine(_home.Path, "views");
        _ = Directory.CreateDirectory(views);
        foreach (var name in new[] { "lensesView", "codeView", "historyView" })
        {
            await File.WriteAllTextAsync(Path.Combine(views, name + ".json"), ScopingFacts.View(name), TestContext.Current.CancellationToken);
        }

        Assert.Equal(0, await Run("scoping", "desk", "gather", "--facts", Facts, "--out", Out,
            "--lenses", Path.Combine(views, "lensesView.json"), "--code", Path.Combine(views, "codeView.json"), "--history", Path.Combine(views, "historyView.json")));

        Assert.True(File.Exists(Path.Combine(Out, "lenses.json")));
        Assert.True(File.Exists(Path.Combine(Out, "coverage.json")));
        var report = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(Out, "report.json"), TestContext.Current.CancellationToken))!;
        Assert.Equal("gathered", report["outcome"]!.GetValue<string>());
        Assert.Equal("coverage.json", report["outputs"]!["fact:coverage"]!.GetValue<string>());
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("check")]
    [InlineData("show")]
    public async Task With_serve_down_an_address_exits_2_with_SND_API_001_and_never_starts_it(string verb)
    {
        Assert.Equal(2, await Run("scoping", verb, "J-1"));

        Assert.Contains("SND-API-001", _error.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(_home.Path, "control.json")));
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("check")]
    [InlineData("show")]
    public async Task Facts_and_an_address_together_or_neither_is_a_usage_error(string verb)
    {
        Assert.Equal(2, await Run("scoping", verb, "J-1", "--facts", Facts));
        Assert.Equal(2, await Run("scoping", verb));
        Assert.Contains("usage: sindri scoping", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_facts_dir_that_does_not_exist_is_exit_2_with_a_fix()
    {
        Assert.Equal(2, await Run("scoping", "check", "--facts", Path.Combine(_home.Path, "nowhere")));

        Assert.Contains("(fix: ", _error.ToString(), StringComparison.Ordinal);
    }

    private bool _session;

    private string? Environment(string name) => name switch { "AW_SINDRI_HOME" => _home.Path, "AW_SINDRI_CHILD" when _session => "1", _ => null };

    private Task<int> Run(params string[] args) => CommandTable.Run(args, Environment, TimeProvider.System);
}
