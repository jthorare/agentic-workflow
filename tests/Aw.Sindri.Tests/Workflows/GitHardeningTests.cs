using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Tests.Workflow.Effects;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The engine's own git calls in an agent's worktree (engine P8 D10, sandbox spec E1-S2): the worktree is the agent's, so hooks, fsmonitor and the ownership check are settled on the command line.</summary>
public sealed class GitHardeningTests
{
    private const string Worktree = "/work/app/.claude/worktrees/sindri-w-1.1";

    [Fact, Trait("Lane", "unit")]
    public void A_worktree_call_turns_hooks_and_fsmonitor_off_and_trusts_only_that_worktree()
    {
        var arguments = GitProcess.Arguments(Worktree, ["status", "--porcelain"], inWorktree: true);

        Assert.Equal(["-C", Worktree, "-c", "core.hooksPath=/dev/null", "-c", "core.fsmonitor=false", "-c", "safe.directory=" + Worktree, "status", "--porcelain"], arguments);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_repository_call_is_left_as_it_was() =>
        Assert.Equal(["-C", "/work/app", "update-ref", "refs/x", "HEAD"], GitProcess.Arguments("/work/app", ["update-ref", "refs/x", "HEAD"], inWorktree: false));

    [Fact, Trait("Lane", "integration")]
    public async Task A_repository_that_configures_an_fsmonitor_command_does_not_run_it_for_the_diff_check()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var ct = TestContext.Current.CancellationToken;
        using var repo = await TempGitRepo.Create();
        var marker = Path.Combine(repo.Path, "..", "fsmonitor-ran");
        var script = Path.Combine(repo.Path, "..", "fsmonitor.sh");
        await File.WriteAllTextAsync(script, $"#!/bin/sh\ntouch '{marker}'\n", ct);
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        _ = await repo.Git("config", "core.fsmonitor", script);
        var diff = new GitRunDiff();
        var head = (await diff.Head(repo.Path, ct)).Value;

        var empty = await diff.IsEmpty(repo.Path, head, ct);

        Assert.True(empty.IsSuccess && empty.Value);
        Assert.False(File.Exists(marker));   // the repository's own command never ran
    }
}
