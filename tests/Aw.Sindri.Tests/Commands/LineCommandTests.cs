using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing.Line;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Commands;

/// <summary>
/// 'sindri design|plan check' and 'sindri line desk' as Program runs them (line spec §4.3, §6.3, Appendix D): the local --facts mode over a directory of packet files,
/// the refusals inside a git worktree, and a serve that is down (SND-API-001, exit 2). It swaps the process's console.
/// </summary>
[Collection(ProcessEnvironment.Name)]
public sealed class LineCommandTests : IDisposable
{
    private const string StubHash = "sha256:00000000000000000000000000000000000000000000000000000000000000ab";

    private readonly TempHome _home = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly TextWriter _consoleOut = Console.Out;
    private readonly TextWriter _consoleError = Console.Error;
    private string? _packets;

    public LineCommandTests()
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

    private string OutDirectory => Path.Combine(_home.Path, "run");

    private string? Environment(string name) => name switch { "AW_SINDRI_HOME" => _home.Path, "AW_PACKETS_BIN" => _packets, _ => null };

    private Task<int> Run(params string[] args) => CommandTable.Run(args, Environment, TimeProvider.System);

    private async Task Put(LineFixture facts, string? directory = null)
    {
        foreach (var (key, text) in facts)
        {
            var path = Path.Combine(directory ?? Facts, key);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, text, TestContext.Current.CancellationToken);
        }
    }

    private string Script(string name, string body) => ExecutableScript.Write(Path.Combine(_home.Path, name), body + "\n");

    [Fact, Trait("Lane", "integration")]
    public async Task Design_check_on_the_gold_packet_exits_0_and_json_prints_the_findings_shape()
    {
        await Put(LineFixture.Design());

        Assert.Equal(0, await Run("design", "check", "--facts", Facts, "--json"));

        using var body = JsonDocument.Parse(_out.ToString());
        Assert.Equal("design", body.RootElement.GetProperty("station").GetString());
        Assert.Equal("draft", body.RootElement.GetProperty("stage").GetString());
        Assert.Equal("pass", body.RootElement.GetProperty("outcome").GetString());
        Assert.Equal(0, body.RootElement.GetProperty("findings").GetArrayLength());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Design_check_on_a_broken_packet_exits_1_and_names_the_rule_with_its_fix()
    {
        await Put(LineFixture.Design().Edit("signals.json", n => n["signals"]!.AsObject().Remove("AC-2")));

        Assert.Equal(1, await Run("design", "check", "--facts", Facts));

        Assert.Contains("DSN-ACC-001", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("fix:", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Design_check_defaults_to_the_draft_stage_and_runs_every_rule_at_evidence()
    {
        await Put(LineFixture.Design().Without("runs.json"));

        Assert.Equal(0, await Run("design", "check", "--facts", Facts));
        _out.GetStringBuilder().Clear();
        Assert.Equal(1, await Run("design", "check", "--facts", Facts, "--stage", "evidence"));
        Assert.Contains("not run: DSN-LENS-002 (needs runs.json)", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_scope_style_lenses_and_repo_flags_supply_what_the_directory_does_not_hold()
    {
        var gold = LineFixture.Design();
        var design = new LineFixture();
        foreach (var (key, text) in gold.Where(p => !p.Key.Contains('/', StringComparison.Ordinal)))
        {
            design[key] = text;
        }

        await Put(design);
        var scope = Path.Combine(_home.Path, "scope");
        await Put(new LineFixture { ["scope-map.json"] = gold["scope/scope-map.json"], ["acceptance.json"] = gold["scope/acceptance.json"], ["questions.json"] = gold["scope/questions.json"], ["answers.jsonl"] = gold["scope/answers.jsonl"], ["lenses.json"] = gold["scope/lenses.json"] }, scope);
        var repo = Path.Combine(_home.Path, "repo");
        await Put(new LineFixture { ["docs/decisions/0001-links-never-expire.md"] = gold["repo/docs/decisions/0001-links-never-expire.md"] }, repo);
        var style = Path.Combine(_home.Path, "style.json");
        await File.WriteAllTextAsync(style, gold["pack/style.json"], TestContext.Current.CancellationToken);
        var lenses = Path.Combine(_home.Path, "lenses.json");
        await File.WriteAllTextAsync(lenses, gold["pack/lenses.json"], TestContext.Current.CancellationToken);

        Assert.Equal(0, await Run("design", "check", "--facts", Facts, "--scope", scope, "--repo", repo, "--style", style, "--lenses", lenses, "--stage", "evidence"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_repo_file_outside_the_repo_dir_is_never_read()
    {
        await Put(LineFixture.Design().Edit("contracts.json", n => n["edits"]![0]!["target"]!["file"] = "../../etc/hostname"));
        var repo = Path.Combine(_home.Path, "repo");
        _ = Directory.CreateDirectory(repo);

        Assert.Equal(1, await Run("design", "check", "--facts", Facts, "--repo", repo));

        Assert.Contains("DSN-XSPEC-001", _out.ToString(), StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("design")]
    [InlineData("plan")]
    public async Task With_serve_down_an_address_exits_2_with_SND_API_001_and_never_starts_it(string station)
    {
        Assert.Equal(2, await Run(station, "check", "J-1"));

        Assert.Contains("SND-API-001", _error.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(_home.Path, "control.json")));
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("design")]
    [InlineData("plan")]
    public async Task Facts_and_an_address_together_or_neither_is_a_usage_error(string station)
    {
        Assert.Equal(2, await Run(station, "check", "J-1", "--facts", Facts));
        Assert.Equal(2, await Run(station, "check"));
        Assert.Contains($"usage: sindri {station} check", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_facts_dir_that_does_not_exist_is_exit_2_with_a_fix()
    {
        Assert.Equal(2, await Run("design", "check", "--facts", Path.Combine(_home.Path, "nowhere")));

        Assert.Contains("(fix: ", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Plan_check_on_the_gold_plan_exits_0_with_the_design_beside_it()
    {
        var gold = LineFixture.Plan();
        var plan = new LineFixture();
        var design = new LineFixture();
        foreach (var (key, text) in gold)
        {
            (key.StartsWith("design/", StringComparison.Ordinal) ? design : plan)[key.StartsWith("design/", StringComparison.Ordinal) ? key[7..] : key] = text;
        }

        await Put(plan);
        var designDirectory = Path.Combine(_home.Path, "design");
        await Put(design, designDirectory);

        Assert.Equal(0, await Run("plan", "check", "--facts", Facts, "--design", designDirectory));
        Assert.Equal(0, await Run("plan", "check", "--facts", Facts, "--design", designDirectory, "--stage", "evidence"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Plan_check_on_a_broken_plan_exits_1()
    {
        await Put(LineFixture.Plan().Edit("tasks.json", n => n["tasks"]![1]!["size"] = "L"));

        Assert.Equal(1, await Run("plan", "check", "--facts", Facts));

        Assert.Contains("PLN-SIZE-001", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Plan_check_asks_aw_packets_to_validate_a_packet_directory_and_reports_what_it_refuses()
    {
        var argv = Path.Combine(_home.Path, "argv.txt");
        _packets = Script("aw-packets-stub", $"printf '%s\\n' \"$@\" > '{argv}'\necho '{{\"passed\":false,\"checks\":[{{\"rule\":\"PKT-SCOPE-002\",\"state\":\"fail\",\"subject\":\"P2,P3\",\"message\":\"two phases write one path\"}}]}}'\nexit 1");
        var gold = LineFixture.Plan().Without("validation.json");
        await Put(gold);
        var repo = Path.Combine(_home.Path, "repo");
        _ = Directory.CreateDirectory(repo);

        Assert.Equal(1, await Run("plan", "check", "--facts", Facts, "--repo", repo));

        Assert.Equal(["validate", "--kind", "plan", Facts, "--repo", repo, "--json"], await File.ReadAllLinesAsync(argv, TestContext.Current.CancellationToken));
        Assert.Contains("PLN-PKT-001", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("PKT-SCOPE-002", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Plan_check_leaves_PLN_PKT_001_not_run_when_aw_packets_is_not_there()
    {
        _packets = Path.Combine(_home.Path, "no-such-aw-packets");
        await Put(LineFixture.Plan().Without("validation.json"));

        Assert.Equal(0, await Run("plan", "check", "--facts", Facts));
        Assert.Contains("not run: PLN-PKT-001 (needs validation.json)", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Line_desk_check_writes_the_report_and_exits_by_the_outcome()
    {
        await Put(LineFixture.Design());

        Assert.Equal(0, await Run("line", "desk", "design.check", "--facts", Facts, "--out", OutDirectory));
        using var passed = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(OutDirectory, "report.json"), TestContext.Current.CancellationToken));
        Assert.Equal("pass", passed.RootElement.GetProperty("outcome").GetString());

        await Put(LineFixture.Design().Edit("signals.json", n => n["signals"]!.AsObject().Remove("AC-2")));
        Assert.Equal(1, await Run("line", "desk", "design.check", "--facts", Facts, "--out", OutDirectory));
        using var gaps = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(OutDirectory, "report.json"), TestContext.Current.CancellationToken));
        Assert.Equal("gaps", gaps.RootElement.GetProperty("outcome").GetString());
        Assert.Equal("findings.json", gaps.RootElement.GetProperty("outputs").GetProperty("fact:findings").GetString());
        Assert.Contains("DSN-ACC-001", await File.ReadAllTextAsync(Path.Combine(OutDirectory, "findings.json"), TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Line_desk_design_evidence_writes_the_packet_and_records_the_hash_aw_packets_prints()
    {
        var argv = Path.Combine(_home.Path, "argv.txt");
        _packets = Script("aw-packets-stub", $"printf '%s\\n' \"$@\" > '{argv}'\necho '{StubHash}'");
        await Put(LineFixture.Design());
        await File.WriteAllTextAsync(Path.Combine(Facts, "scopePacket.json"), "{\"hash\":\"sha256:48b9dc81f8935c81f465f785a6e7dba0abd9697480786ad7994d1475da00c973\"}", TestContext.Current.CancellationToken);

        Assert.Equal(0, await Run("line", "desk", "design.evidence", "--facts", Facts, "--out", OutDirectory));

        Assert.Equal(["hash", "--kind", "design", Path.Combine(OutDirectory, "design-packet")], await File.ReadAllLinesAsync(argv, TestContext.Current.CancellationToken));
        using var packet = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(OutDirectory, "designPacket.json"), TestContext.Current.CancellationToken));
        Assert.Equal(StubHash, packet.RootElement.GetProperty("hash").GetString());
        Assert.Equal(13, Directory.GetFiles(Path.Combine(OutDirectory, "design-packet")).Length);
        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(OutDirectory, "report.json"), TestContext.Current.CancellationToken));
        Assert.Equal("designPacket.json", report.RootElement.GetProperty("outputs").GetProperty("fact:designPacket").GetString());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Line_desk_plan_evidence_takes_the_parents_from_the_facts_dir_or_the_flags()
    {
        _packets = Script("aw-packets-stub", $"echo '{StubHash}'");
        var gold = LineFixture.Plan();
        await Put(gold);
        await File.WriteAllTextAsync(Path.Combine(Facts, "scopePacket.json"), "{\"hash\":\"sha256:48b9dc81f8935c81f465f785a6e7dba0abd9697480786ad7994d1475da00c973\"}", TestContext.Current.CancellationToken);

        Assert.Equal(2, await Run("line", "desk", "plan.evidence", "--facts", Facts, "--out", OutDirectory));   // no design hash yet
        Assert.Equal(0, await Run("line", "desk", "plan.evidence", "--facts", Facts, "--out", OutDirectory, "--design-hash", "sha256:3deb049307bf38e0981ed4ed374f7d84a63f305f0067a247c10f839dfd4f87e4", "--parallel", "1"));

        var index = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(OutDirectory, "plan-packet", "index.json"), TestContext.Current.CancellationToken))!;
        Assert.Equal("sha256:3deb049307bf38e0981ed4ed374f7d84a63f305f0067a247c10f839dfd4f87e4", (string?)index["parents"]![0]);
        Assert.Equal("[[\"P1\"],[\"P2\"],[\"P3\"],[\"P4\"]]", JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(OutDirectory, "plan-packet", "graph.json"), TestContext.Current.CancellationToken))!["waves"]!.ToJsonString());
        Assert.True(File.Exists(Path.Combine(OutDirectory, "planPacket.json")));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Line_desk_refuses_an_out_dir_inside_a_git_worktree()
    {
        await Put(LineFixture.Design());
        var worktree = Path.Combine(_home.Path, "checkout");
        _ = Directory.CreateDirectory(worktree);
        await File.WriteAllTextAsync(Path.Combine(worktree, ".git"), "gitdir: /elsewhere\n", TestContext.Current.CancellationToken);

        Assert.Equal(2, await Run("line", "desk", "design.check", "--facts", Facts, "--out", Path.Combine(worktree, "out")));

        Assert.Contains("SND-SCOPING-002", _error.ToString(), StringComparison.Ordinal);
        Assert.Empty(_out.ToString());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Line_desk_with_a_block_it_does_not_have_or_no_arguments_is_exit_2()
    {
        await Put(LineFixture.Design());

        Assert.Equal(2, await Run("line", "desk", "design.draft", "--facts", Facts, "--out", OutDirectory));
        Assert.Equal(2, await Run("line", "desk"));
        Assert.Equal(2, await Run("line"));
        Assert.Contains("usage: sindri line desk", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Design_show_reads_a_design_packet_and_names_the_plan_sections_it_does_not_have_yet()
    {
        await Put(LineFixture.Design());

        Assert.Equal(0, await Run("design", "show", "--facts", Facts, "--json"));

        var view = JsonNode.Parse(_out.ToString())!;
        Assert.Equal(3, view["decisions"]!.AsArray().Count);
        Assert.Equal(["graph", "trace"], view["notYet"]!.AsArray().Select(n => (string?)n), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Plan_show_with_the_design_beside_it_is_the_full_stop_view()
    {
        var gold = LineFixture.Plan();
        var plan = new LineFixture();
        var design = new LineFixture();
        foreach (var (key, text) in gold)
        {
            (key.StartsWith("design/", StringComparison.Ordinal) ? design : plan)[key.StartsWith("design/", StringComparison.Ordinal) ? key[7..] : key] = text;
        }

        await Put(plan);
        var designDirectory = Path.Combine(_home.Path, "design");
        await Put(design, designDirectory);

        Assert.Equal(0, await Run("plan", "show", "--facts", Facts, "--design", designDirectory, "--json"));

        var view = JsonNode.Parse(_out.ToString())!;
        Assert.Empty(view["notYet"]!.AsArray());
        Assert.Equal(4, (int)view["graph"]!["prCount"]!);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Show_in_text_has_the_seven_sections_in_the_order_the_owner_reads_them()
    {
        await Put(LineFixture.Plan());

        Assert.Equal(0, await Run("plan", "show", "--facts", Facts));

        var text = _out.ToString();
        string[] headings = ["Accepted risks", "Models", "Decisions", "Acceptance", "Plan graph", "Trace", "Coverage", "Spec", "Contract edits", "Lens rounds"];
        Assert.Equal(headings.Order(Comparer<string>.Create((a, b) => text.IndexOf(a, StringComparison.Ordinal).CompareTo(text.IndexOf(b, StringComparison.Ordinal)))), headings, StringComparer.Ordinal);
        Assert.All(headings, h => Assert.Contains(h, text, StringComparison.Ordinal));
        Assert.Contains("D3 (answered, chose D3.a, recommended D3.a)", text, StringComparison.Ordinal);
        Assert.Contains("P2 [S, feature] stack 1, wave 2, base dependency-head P1", text, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("design")]
    [InlineData("plan")]
    public async Task Show_with_serve_down_exits_2_with_SND_API_001_and_a_facts_dir_is_needed_otherwise(string station)
    {
        Assert.Equal(2, await Run(station, "show", "J-1"));
        Assert.Contains("SND-API-001", _error.ToString(), StringComparison.Ordinal);
        Assert.Equal(2, await Run(station, "show"));
        Assert.Contains($"usage: sindri {station} show", _error.ToString(), StringComparison.Ordinal);
    }
}
