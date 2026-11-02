using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Sandbox.Denials;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Tests.Sandbox.Plan;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Denials;

/// <summary>Reading denials from transcript tails and failopen.log (sandbox spec §3.6, §15).</summary>
[Lane(TestLane.Unit)]
public sealed class TranscriptDenialsTests
{
    private const string Home = "/home/dev";

    private static string[] Fixture(string name) => File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Sandbox", "Denials", "Fixtures", name));

    private static SandboxPlan Plan() => SandboxPlannerTests.Build(Req.WriteScope) with
    {
        Mounts =
        [
            new PlanMount(Home, PlanMountKind.Tmpfs),
            new PlanMount(Home + "/work", PlanMountKind.ReadWrite),
            new PlanMount(Home + "/.ssh", PlanMountKind.Deny),
        ],
    };

    [Fact]
    public void Claude_permission_denial_reads_as_policy()
    {
        var read = ClaudeTranscriptDenials.Read(Fixture("claude-tail.jsonl"));

        var targets = read.Observations.Select(o => (o.Kind, o.Target, o.Rule)).ToList();
        Assert.Contains((DenialKind.Policy, "Bash: git push", DenialRules.InnerPolicy), targets);
        Assert.Contains((DenialKind.Policy, "Read", DenialRules.InnerPolicy), targets);
        Assert.Contains((DenialKind.Policy, "WebFetch", DenialRules.InnerPolicy), targets);
        Assert.Equal(3, read.Observations.Count);
    }

    [Fact]
    public void Command_line_is_never_the_target()
    {
        var read = ClaudeTranscriptDenials.Read(Fixture("claude-tail.jsonl"));

        Assert.DoesNotContain(read.Observations, o => o.Target.Contains("origin", StringComparison.Ordinal) || o.Target.Contains("main", StringComparison.Ordinal));
    }

    [Fact]
    public void Claude_read_only_fs_error_on_a_denied_path_reads_as_deny_list()
    {
        var read = ClaudeTranscriptDenials.Read(Fixture("claude-tail.jsonl"));

        Assert.Contains(read.FailedPaths, p => p == Home + "/.ssh/probe");
        var match = PathDenials.Match(Plan(), Home + "/.ssh/probe", Home);
        Assert.Equal(new DenialObservation(DenialKind.Path, Home + "/.ssh/probe", DenialRules.DenyList), match);
    }

    [Fact]
    public void Failed_path_under_home_is_matched_as_not_bound()
    {
        var match = PathDenials.Match(Plan(), Home + "/.aws/credentials", Home);

        Assert.Equal(new DenialObservation(DenialKind.Path, Home + "/.aws/credentials", DenialRules.NotBound), match);
        Assert.Null(PathDenials.Match(Plan(), Home + "/work/src/a.cs", Home));
    }

    [Fact]
    public void Missing_file_outside_home_is_not_a_denial()
    {
        var read = ClaudeTranscriptDenials.Read(Fixture("claude-tail.jsonl"));

        Assert.Contains(read.FailedPaths, p => p == "/var/data/missing.txt");
        Assert.Null(PathDenials.Match(Plan(), "/var/data/missing.txt", Home));
        Assert.Null(PathDenials.Match(Plan(), Home + "/work/../../etc/passwd", Home));
    }

    [Fact]
    public void Codex_exec_error_reads_as_path_candidate()
    {
        var read = CodexTranscriptDenials.Read(Fixture("codex-tail.jsonl"));

        Assert.Empty(read.Observations);
        Assert.Equal([Home + "/.aws/credentials", Home + "/notes/todo.md", Home + "/.bashrc"], read.FailedPaths);
    }

    [Fact]
    public void Bad_json_lines_are_skipped()
    {
        string[] lines = ["", "not json", "[1,2]", "{\"type\":", .. Fixture("claude-tail.jsonl")];

        Assert.Equal(3, ClaudeTranscriptDenials.Read(lines).Observations.Count);
        Assert.Empty(CodexTranscriptDenials.Read(["{", "null", "{\"payload\":5}"]).FailedPaths);
        Assert.Empty(FailOpenLog.Read(["{", "{\"event\":1}"]));
    }

    [Fact]
    public void Fail_open_log_reads_as_hook_fail_open()
    {
        var read = FailOpenLog.Read(Fixture("failopen.log"));

        Assert.Equal(
            [new DenialObservation(DenialKind.Policy, "send-gate", DenialRules.HookFailOpen), new DenialObservation(DenialKind.Policy, "scope-gate", DenialRules.HookFailOpen)],
            read);
    }
}
