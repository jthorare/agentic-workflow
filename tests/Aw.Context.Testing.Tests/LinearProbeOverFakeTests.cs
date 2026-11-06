using System.Diagnostics;
using System.Text.Json.Nodes;
using Aw.Context.Testing.Linear;
using Aw.Kernel.Testing;

namespace Aw.Context.Testing.Tests;

/// <summary>scripts/linear-probe.sh run end to end against the stateful fake Linear (spec 2026-11-04-sindri-linear-hub-design.md §12 Ph0): the two halves of Ph0 agree on the queries, the answer shapes and the cleanup.</summary>
[Lane(TestLane.Integration)]
public sealed class LinearProbeOverFakeTests
{
    private const string ReadToken = "fixture-linear-read";
    private const string WriteToken = "fixture-linear-write";
    private const string Marker = "zebrafish";
    private const string ProbeLabel = "sindri-probe";

    [Fact]
    public async Task Read_only_prints_the_schema_and_rate_limit_facts_and_sends_no_mutation()
    {
        await using var linear = await Start();

        var run = await ProbeWithWriteKey(linear, writeKey: "", "--url", linear.Url.ToString());

        Assert.True(run.ExitCode == 0, run.Text);
        foreach (var line in new[]
        {
            "mutation.issueCreate: yes", "mutation.issueUpdate: yes", "mutation.commentCreate: yes", "mutation.commentUpdate: yes",
            "mutation.attachmentLinkURL: yes", "mutation.issueArchive: yes", "schema.IssueCreateInput.id: yes", "schema.CommentCreateInput.id: yes",
            "schema.IssueUpdateInput.conditional: none", "key.scope: unknown", "ratelimit.read.requests_limit: 5000", "ratelimit.write: skipped (no write key)",
        })
        {
            Assert.Contains(line + "\n", run.Stdout, StringComparison.Ordinal);
        }

        Assert.Contains("ratelimit.read.requests_remaining: ", run.Stdout, StringComparison.Ordinal);
        Assert.Contains("ratelimit.read.requests_reset: ", run.Stdout, StringComparison.Ordinal);
        Assert.Contains("ratelimit.shared_between_keys: unknown", run.Stdout, StringComparison.Ordinal);
        Assert.Contains("mention: skipped", run.Stdout, StringComparison.Ordinal);
        Assert.Empty(MutationBodies(linear));
        NothingSecretIn(run);
    }

    [Fact]
    public async Task Read_only_with_a_team_counts_the_mention_syntaxes_in_its_comments()
    {
        await using var linear = await Start();

        var run = await Probe(linear, "--url", linear.Url.ToString(), "--team", "ENG");

        Assert.True(run.ExitCode == 0, run.Text);
        Assert.Contains("mention.scanned: 4\n", run.Stdout, StringComparison.Ordinal);
        Assert.Contains("mention.at_name: 1\n", run.Stdout, StringComparison.Ordinal);
        Assert.Contains("mention.profile_url: 1\n", run.Stdout, StringComparison.Ordinal);
        Assert.Contains("mention.other: 1\n", run.Stdout, StringComparison.Ordinal);
        Assert.Empty(MutationBodies(linear));
        NothingSecretIn(run);
    }

    [Fact]
    public async Task The_write_part_finds_the_write_facts_and_leaves_no_probe_issue()
    {
        await using var linear = await Start();
        var issuesBefore = linear.Issues.Count;

        var run = await Probe(linear, "--url", linear.Url.ToString(), "--team", "ENG", "--write");

        Assert.True(run.ExitCode == 0, run.Text);
        foreach (var line in new[]
        {
            "label: created", "write.allowed: yes", "create.client_id: accepted", "create.repeat_client_id: INVALID_INPUT", "comment.client_id: accepted",
            "comment.repeat_client_id: INVALID_INPUT", "updatedAt.bumped_by.comment: yes", "attachment.repeat_url: one", "updatedAt.bumped_by.attachment: yes",
            "updatedAt.bumped_by.update: yes", "cap.title_300: ok", "cap.description_70k: ok", "cleanup.left: 0",
            "archived.hidden_by_default: yes", "pagination.after_cursor: ok", "team_move.old_key_resolves: not probed",
        })
        {
            Assert.Contains(line + "\n", run.Stdout, StringComparison.Ordinal);
        }

        Assert.Empty(LiveProbeIssues(linear));
        Assert.Equal(2, linear.Issues.Count - issuesBefore);   // why: the probe's issue and its 300-character-title one, both archived
        foreach (var issue in linear.Issues.Skip(issuesBefore))
        {
            Assert.NotNull(issue.ArchivedAt);
        }

        Assert.Contains(linear.RequestBodies, IsMutation);
        NothingSecretIn(run);
    }

    [Fact]
    public async Task An_issue_with_the_probe_label_that_the_run_did_not_make_is_left_alone()
    {
        await using var linear = await Start(seed => seed
            .Label(ProbeLabel, "ENG")
            .Issue("ENG", "Someone's own probe-labelled issue", new FakeLinearIssueSeed { Labels = [ProbeLabel] }));

        var run = await Probe(linear, "--url", linear.Url.ToString(), "--team", "ENG", "--write");

        Assert.True(run.ExitCode == 0, run.Text);
        Assert.Contains("warning: 1 sindri-probe issues existed before this run; left alone", run.Stderr, StringComparison.Ordinal);
        var live = LiveProbeIssues(linear);
        var kept = Assert.Single(live);
        Assert.Contains("probe-labelled", kept.Title, StringComparison.Ordinal);
        NothingSecretIn(run);
    }

    [Fact]
    public async Task A_lost_answer_fails_the_run_and_the_cleanup_still_archives_the_probe_issue()
    {
        await using var linear = await Start(seed => seed.Label(ProbeLabel, "ENG"));
        linear.Switches.NoAnswer = true;   // why: the first mutation is the probe's issueCreate; it is applied, then the connection drops

        var run = await Probe(linear, "--url", linear.Url.ToString(), "--team", "ENG", "--write");

        Assert.Equal(1, run.ExitCode);
        Assert.Contains("failed: issue create", run.Stderr, StringComparison.Ordinal);
        Assert.Contains("cleanup.left: 0\n", run.Stdout, StringComparison.Ordinal);
        Assert.Contains("cleanup: archived 1 of 1\n", run.Stdout, StringComparison.Ordinal);
        Assert.Empty(LiveProbeIssues(linear));
        _ = Assert.Single(linear.Issues, i => i.Labels.Contains(ProbeLabel, StringComparer.Ordinal));
        NothingSecretIn(run);
    }

    [Fact]
    public async Task A_write_key_equal_to_the_read_key_is_refused_before_any_request()
    {
        await using var linear = await Start();

        var run = await ProbeWithWriteKey(linear, ReadToken, "--url", linear.Url.ToString(), "--team", "ENG", "--write");

        Assert.Equal(2, run.ExitCode);
        Assert.Empty(linear.RequestBodies);
        NothingSecretIn(run);
    }

    private static Task<FakeLinear> Start(Action<FakeLinearSeed>? more = null) =>
        FakeLinear.Start(
            seed =>
            {
                _ = seed.Team("ENG").Member("Ada Fixture")
                    .Issue("ENG", $"Fix the {Marker} login timeout", new FakeLinearIssueSeed { Description = $"The {Marker} times out after ten seconds." })
                    .Issue("ENG", $"Rename the {Marker} export")
                    .Comment("ENG-1", $"@ada the {Marker} fix is in")
                    .Comment("ENG-1", $"see https://linear.app/acme/profiles/ada {Marker}")
                    .Comment("ENG-2", $"mail ada@example.test about the {Marker}")
                    .Comment("ENG-2", $"plain note about the {Marker}");
                more?.Invoke(seed);
            },
            ReadToken,
            WriteToken);

    private static List<FakeLinearIssue> LiveProbeIssues(FakeLinear linear) =>
        [.. linear.Issues.Where(i => i.ArchivedAt is null && i.Labels.Contains(ProbeLabel, StringComparer.Ordinal))];

    private static List<string> MutationBodies(FakeLinear linear) => [.. linear.RequestBodies.Where(IsMutation)];

    private static bool IsMutation(string body) =>
        JsonNode.Parse(body)?["query"]?.GetValue<string>().TrimStart().StartsWith("mutation", StringComparison.Ordinal) == true;

    private static void NothingSecretIn(ProbeRun run)
    {
        foreach (var secret in new[] { ReadToken, WriteToken, Marker })
        {
            Assert.DoesNotContain(secret, run.Text, StringComparison.Ordinal);
        }
    }

    private static Task<ProbeRun> Probe(FakeLinear linear, params string[] args) => ProbeWithWriteKey(linear, WriteToken, args);

    private static async Task<ProbeRun> ProbeWithWriteKey(FakeLinear linear, string writeKey, params string[] args)
    {
        ArgumentNullException.ThrowIfNull(linear);
        var home = Directory.CreateTempSubdirectory("aw-linear-probe-");
        try
        {
            var start = new ProcessStartInfo("bash") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            start.ArgumentList.Add(Path.Combine(RepoRoot(), "scripts", "linear-probe.sh"));
            foreach (var arg in args)
            {
                start.ArgumentList.Add(arg);
            }

            foreach (var name in start.Environment.Keys.Where(k => k.StartsWith("AW_META_LANE", StringComparison.Ordinal) || k.StartsWith("AW_LINEAR_PROBE", StringComparison.Ordinal)).ToList())
            {
                _ = start.Environment.Remove(name);
            }

            foreach (var name in new[] { "AW_SINDRI_CHILD", "AW_SINDRI_HOME", "TMUX" })
            {
                _ = start.Environment.Remove(name);
            }

            start.Environment["HOME"] = home.FullName;
            start.Environment["AW_STATE_DIR"] = Path.Combine(home.FullName, "state");
            start.Environment["AW_LINEAR_PROBE_KEY"] = linear.Token;
            start.Environment["AW_LINEAR_PROBE_WRITE_KEY"] = writeKey;

            using var process = Process.Start(start)!;   // why: the program is bash, which starts
            var stdout = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            return new ProbeRun(process.ExitCode, await stdout, await stderr);
        }
        finally
        {
            home.Delete(recursive: true);
        }
    }

    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AgenticWorkflow.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("AgenticWorkflow.slnx not found above the test assembly");
    }

    private sealed record ProbeRun(int ExitCode, string Stdout, string Stderr)
    {
        public string Text => Stdout + Stderr;
    }
}
