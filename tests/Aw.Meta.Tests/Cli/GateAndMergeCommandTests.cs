using System.Diagnostics;
using Aw.Kernel.Testing;
using Aw.Meta.Tests.MergeRun;

namespace Aw.Meta.Tests.Cli;

/// <summary>gate-run, merge-check and merge-run end to end: real git (a bare origin, a clone, a pull request head ref) and a stub gh. Pull request 7's head branch "feat" is the team's (watch.prBranchPrefixes), so the watch reports it.</summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class GateAndMergeCommandTests : IDisposable
{
    private static readonly string[] _gitIdentity = ["-c", "user.name=t", "-c", "user.email=t@t", "-c", "commit.gpgsign=false", "-c", "init.defaultBranch=main"];

    private static readonly string _config = string.Concat(
        """{"team":"t","repo":"REPO","lanes":["api"],"metaLane":"meta","watch":{"prBranchPrefixes":["feat"]},"gate":{"always":["repo"],"docsOnly":["**/*.md"],"scopes":{""",
        """" "repo":{"paths":["**"],"steps":[{"name":"merged","run":["sh","-c","test -f feature.txt && test -f main.txt && echo both-present"]}]},"""",
        """" "extra":{"paths":["src/**"],"steps":[{"name":"red","run":["sh","-c","echo boom; exit 3"]}]}}}}"""");

    private readonly CliHarness _cli = new();
    private string _repo = string.Empty;
    private string _head = string.Empty;

    private static void Git(string directory, params string[] args)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in _gitIdentity.Concat(args))
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info) ?? throw new InvalidOperationException("git did not start");
        _ = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', args)}: {error}");
        }
    }

    // A bare origin with main (main.txt), a pull request head at refs/pull/7/head (main.txt + feature.txt), and a clone to work in.
    private async Task Setup(string changedPath = "feature.txt")
    {
        var origin = _cli.File("origin.git");
        _repo = _cli.File("work");
        _ = Directory.CreateDirectory(origin);
        Git(origin, "init", "--bare", "--quiet");
        _ = Directory.CreateDirectory(_repo);
        Git(_repo, "init", "--quiet");
        Git(_repo, "remote", "add", "origin", origin);
        await File.WriteAllTextAsync(Path.Combine(_repo, "main.txt"), "main", TestContext.Current.CancellationToken);
        Git(_repo, "add", ".");
        Git(_repo, "commit", "--quiet", "-m", "main");
        Git(_repo, "push", "--quiet", "origin", "HEAD:refs/heads/main");
        Git(_repo, "checkout", "--quiet", "-b", "feat");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(_repo, changedPath))!);
        await File.WriteAllTextAsync(Path.Combine(_repo, changedPath), "feature", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_repo, "feature.txt"), "feature", TestContext.Current.CancellationToken);
        Git(_repo, "add", ".");
        Git(_repo, "commit", "--quiet", "-m", "feature");
        Git(_repo, "push", "--quiet", "origin", "HEAD:refs/pull/7/head");
        _head = HeadSha(_repo);
        Git(_repo, "checkout", "--quiet", "main");
        await File.WriteAllTextAsync(_cli.File("team.json"), _config.Replace("REPO", _repo, StringComparison.Ordinal), TestContext.Current.CancellationToken);
        _cli.SetEnv("AW_META_CONFIG", _cli.File("team.json"));
        WriteGh(draft: false);
    }

    private static string HeadSha(string directory)
    {
        var info = new ProcessStartInfo("git", ["rev-parse", "HEAD"]) { WorkingDirectory = directory, RedirectStandardOutput = true, UseShellExecute = false };
        using var process = Process.Start(info) ?? throw new InvalidOperationException("git did not start");
        var sha = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return sha;
    }

    private void WriteGh(bool draft)
    {
        var script = _cli.File("gh");
        var view = string.Concat("""'{"headRefOid":""", "\"", _head, "\"", ""","headRefName":"feat","isDraft":""", draft ? "true" : "false", ""","title":"Add feature","state":"OPEN"}'""");
        var list = string.Concat("""'[{"number":7,"state":"OPEN","isDraft":false,"headRefOid":""", "\"", _head, "\"", ""","headRefName":"feat","mergeable":"MERGEABLE","title":"Add feature"}]'""");
        _ = ExecutableScript.Write(
            script,
            string.Join('\n',
            [
                "case \"$1 $2\" in",
                "  \"pr view\") echo " + view + " ;;",
                "  \"api graphql\") echo '" + QueueAnswers.Off + "' ;;",   // no merge queue on main
                "  \"pr merge\") exit 0 ;;",
                "  \"pr list\") echo " + list + " ;;",
                "  *) echo \"unexpected gh $*\" >&2; exit 9 ;;",
                "esac",
            ]) + "\n");
        _cli.SetEnv("AW_GH", script);
        _cli.SetEnv("AW_HEAVY", _cli.File("no-such-heavy"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_pull_request_is_gated_in_its_own_tree_with_the_head_merged_onto_main_then_checked_and_merged()
    {
        await Setup(changedPath: "docs/readme.md");

        var gate = await _cli.Run("gate-run", "7");
        var check = await _cli.Run("merge-check", "7");
        var merge = await _cli.Run("merge-run", "7");
        var status = await _cli.Run("status");

        Assert.Equal(0, gate.Exit);
        Assert.Contains("PASS merged", gate.Out, StringComparison.Ordinal);
        Assert.Contains("RESULT pr=7 PASS", gate.Out, StringComparison.Ordinal);
        Assert.Contains("scopes: repo", gate.Out, StringComparison.Ordinal);   // nothing under src/ changed, so only the always scope runs
        Assert.Contains("both-present", await File.ReadAllTextAsync(Path.Combine(_cli.File("meta"), "gates", "7", "1", "merged.log"), TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal(0, check.Exit);
        Assert.Contains("PASS gate-result: PASS", check.Out, StringComparison.Ordinal);
        Assert.Contains("READY #7", check.Out, StringComparison.Ordinal);
        Assert.Equal(0, merge.Exit);
        Assert.Contains("MERGED #7: main is now ", merge.Out, StringComparison.Ordinal);
        Assert.Contains("#7 Add feature merged. Merge main into your branches.", merge.Out, StringComparison.Ordinal);
        Assert.Contains("health: never run", status.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_red_step_fails_the_gate_and_blocks_the_merge()
    {
        await Setup(changedPath: "src/code.txt");

        var gate = await _cli.Run("gate-run", "7");
        var check = await _cli.Run("merge-check", "7");
        var merge = await _cli.Run("merge-run", "7");

        Assert.Equal(1, gate.Exit);
        Assert.Contains("FAIL red", gate.Out, StringComparison.Ordinal);
        Assert.Contains("RESULT pr=7 FAIL", gate.Out, StringComparison.Ordinal);
        Assert.Contains("boom", await File.ReadAllTextAsync(Path.Combine(_cli.File("meta"), "gates", "7", "1", "red.log"), TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal(1, check.Exit);
        Assert.Contains("FAIL gate-result", check.Out, StringComparison.Ordinal);
        Assert.Equal(1, merge.Exit);
        Assert.Contains("NOT MERGED #7", merge.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Main_moving_after_the_gate_refuses_the_merge()
    {
        await Setup(changedPath: "docs/readme.md");
        _ = await _cli.Run("gate-run", "7");
        await File.WriteAllTextAsync(Path.Combine(_repo, "later.txt"), "later", TestContext.Current.CancellationToken);
        Git(_repo, "add", ".");
        Git(_repo, "commit", "--quiet", "-m", "later");
        Git(_repo, "push", "--quiet", "origin", "HEAD:refs/heads/main");

        var check = await _cli.Run("merge-check", "7");

        Assert.Equal(1, check.Exit);
        Assert.Contains("FAIL main:", check.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_pull_request_that_was_never_gated_is_refused_and_a_bad_number_is_a_usage_error()
    {
        await Setup();

        var check = await _cli.Run("merge-check", "7");
        var bad = await _cli.Run("merge-check", "seven");
        var scope = await _cli.Run("gate-run", "7", "nope");

        Assert.Equal(1, check.Exit);
        Assert.Contains("FAIL gate: no gate recorded", check.Out, StringComparison.Ordinal);
        Assert.Equal(2, bad.Exit);
        Assert.StartsWith("AW-META-001: ", bad.Err, StringComparison.Ordinal);
        Assert.Equal(2, scope.Exit);
        Assert.Contains("no gate scope 'nope'", scope.Err, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Watch_prints_the_baseline_and_asks_to_be_rearmed_and_health_is_kept_for_status()
    {
        await Setup();
        var watch = await _cli.Run("watch", "--interval", "5", "--for", "5");
        var health = await _cli.Run("health");
        var status = await _cli.Run("status");

        Assert.Equal(0, watch.Exit);
        Assert.StartsWith("WATCH started: 1 open PR(s), main ", watch.Out, StringComparison.Ordinal);
        Assert.EndsWith("WATCH EXPIRING: rerun aw-meta watch" + Environment.NewLine, watch.Out, StringComparison.Ordinal);
        Assert.InRange(health.Exit, 0, 1);
        Assert.Contains("HEALTH ", health.Out, StringComparison.Ordinal);
        Assert.Matches(@"health: \S+ (ok|attention): orphans=\d+ ", status.Out);
    }

    public void Dispose() => _cli.Dispose();
}
