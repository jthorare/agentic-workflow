using System.Diagnostics;
using System.Globalization;
using System.Text;
using Aw.Kernel.Testing;

namespace Aw.Meta.Tests.Cli;

/// <summary>
/// A scratch GitHub world for the worktree commands: a bare origin, the team's shared checkout (a clone), branches, worktrees under a
/// worktrees root, processes sitting in some of them, and a stub gh that answers from JSON files the test writes.
/// </summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
internal sealed class WorktreeLab : IDisposable
{
    private static readonly string[] _identity = ["-c", "user.name=t", "-c", "user.email=t@t", "-c", "commit.gpgsign=false", "-c", "init.defaultBranch=main"];
    private readonly List<Process> _sleepers = [];

    internal WorktreeLab()
    {
        Cli = new CliHarness();
    }

    internal CliHarness Cli { get; }

    internal string Origin => Cli.File("origin.git");

    internal string Repo => Cli.File("work");

    internal string Root => Cli.File("aw-wt");

    internal string GhState => Cli.File("gh-state");

    internal static void Git(string directory, params string[] args) => _ = GitOut(directory, args);

    internal static string GitOut(string directory, params string[] args)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in _identity.Concat(args))
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info) ?? throw new InvalidOperationException("git did not start");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode == 0 ? output.Trim() : throw new InvalidOperationException($"git {string.Join(' ', args)}: {error}");
    }

    /// <summary>A bare origin and the shared checkout with one commit on main (and a .gitignore for bin/).</summary>
    internal async Task Init()
    {
        _ = Directory.CreateDirectory(Origin);
        Git(Origin, "init", "--bare", "--quiet");
        _ = Directory.CreateDirectory(Repo);
        _ = Directory.CreateDirectory(Root);
        Git(Repo, "init", "--quiet");
        Git(Repo, "remote", "add", "origin", Origin);
        await File.WriteAllTextAsync(Path.Combine(Repo, "main.txt"), "main", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(Repo, ".gitignore"), "bin/\n", TestContext.Current.CancellationToken);
        Git(Repo, "add", ".");
        Git(Repo, "commit", "--quiet", "-m", "main");
        Git(Repo, "push", "--quiet", "origin", "HEAD:refs/heads/main");
        Git(Repo, "fetch", "--quiet", "origin");
        _ = Directory.CreateDirectory(GhState);
        _ = Directory.CreateDirectory(Cli.File("elsewhere"));
        Cli.SetEnv("AW_HEAVY", Cli.File("no-such-heavy"));
    }

    /// <summary>A branch with one commit, pushed to origin (and as the head of pull request <paramref name="pr"/> when given). The shared checkout stays on main.</summary>
    internal async Task<string> Branch(string name, bool push = true, int? pr = null)
    {
        Git(Repo, "checkout", "--quiet", "-b", name, "main");
        await File.WriteAllTextAsync(Path.Combine(Repo, name.Replace('/', '-') + ".txt"), name, TestContext.Current.CancellationToken);
        Git(Repo, "add", ".");
        Git(Repo, "commit", "--quiet", "-m", name);
        if (push)
        {
            Git(Repo, "push", "--quiet", "origin", name + ":refs/heads/" + name);
        }

        if (pr is { } number)
        {
            Git(Repo, "push", "--quiet", "origin", string.Create(CultureInfo.InvariantCulture, $"{name}:refs/pull/{number}/head"));
        }

        var sha = GitOut(Repo, "rev-parse", "HEAD");
        Git(Repo, "checkout", "--quiet", "main");
        return sha;
    }

    /// <summary>Merges a branch into main with a merge commit and pushes main.</summary>
    internal void MergeIntoMain(string name)
    {
        Git(Repo, "merge", "--quiet", "--no-ff", "--no-edit", name);
        Git(Repo, "push", "--quiet", "origin", "main:refs/heads/main");
    }

    /// <summary>Adds a worktree for an existing branch under <paramref name="root"/> (the worktrees root by default).</summary>
    internal string Tree(string name, string? root = null)
    {
        var path = Path.Combine(root ?? Root, name.Replace('/', '-'));
        Git(Repo, "worktree", "add", "--quiet", path, name);
        return path;
    }

    /// <summary>Adds a commit to a worktree's branch without pushing it.</summary>
    internal static async Task CommitLocally(string tree, string file)
    {
        await File.WriteAllTextAsync(Path.Combine(tree, file), "local", TestContext.Current.CancellationToken);
        Git(tree, "add", ".");
        Git(tree, "commit", "--quiet", "-m", "unpushed " + file);
    }

    /// <summary>Starts a process whose working directory is <paramref name="directory"/>; <see cref="StopSleepers"/> ends them.</summary>
    internal void StartSleeper(string directory)
    {
        var info = new ProcessStartInfo("sleep") { UseShellExecute = false, WorkingDirectory = directory };
        info.ArgumentList.Add("300");
        _sleepers.Add(Process.Start(info) ?? throw new InvalidOperationException("sleep did not start"));
    }

    internal void StopSleepers()
    {
        foreach (var process in _sleepers)
        {
            if (!process.HasExited)
            {
                process.Kill();
            }

            process.WaitForExit();
            process.Dispose();
        }

        _sleepers.Clear();
    }

    // When every stub pull request "ended".
    private string Ended => (EndedAt ?? DateTimeOffset.Parse("2999-01-01T00:00:00Z", CultureInfo.InvariantCulture)).ToString("O", CultureInfo.InvariantCulture);

    /// <summary>When the stub pull requests merged or closed; unset, long after every file in the lab (rule 7 compares a tree with the later of this and the sweep marker, so a test about the marker sets it early).</summary>
    internal DateTimeOffset? EndedAt { get; set; }

    /// <summary>Writes what the stub gh answers: `gh pr list` prints the list, `gh pr view N` the pull request, `gh pr merge N` runs merge-N.sh when it exists.</summary>
    internal async Task Gh(params (int Number, string State, string Branch)[] prs)
    {
        var list = new StringBuilder("[");
        foreach (var (number, state, branch) in prs)
        {
            var head = GitOut(Repo, "rev-parse", "--verify", branch);
            var row = string.Create(CultureInfo.InvariantCulture, $$"""{"number":{{number}},"state":"{{state}}","isDraft":false,"headRefOid":"{{head}}","headRefName":"{{branch}}","mergeable":"MERGEABLE","title":"t{{number}}","mergedAt":"{{Ended}}","closedAt":"{{Ended}}"}""");
            _ = list.Append(list.Length > 1 ? "," : string.Empty).Append(row);
            await File.WriteAllTextAsync(Path.Combine(GhState, string.Create(CultureInfo.InvariantCulture, $"view-{number}.json")), row, TestContext.Current.CancellationToken);
        }

        await File.WriteAllTextAsync(Path.Combine(GhState, "list.json"), list.Append(']').ToString(), TestContext.Current.CancellationToken);
        var script = Cli.File("gh");
        _ = ExecutableScript.Write(
            script,
            string.Join('\n',
            [
                $"S='{GhState}'",
                "case \"$1 $2\" in",
                "  \"pr list\") cat \"$S/list.json\" ;;",
                "  \"pr view\") cat \"$S/view-$3.json\" ;;",
                "  \"pr merge\") if [ -f \"$S/merge-$3.sh\" ]; then sh \"$S/merge-$3.sh\"; fi ;;",
                "  *) echo \"unexpected gh $*\" >&2; exit 9 ;;",
                "esac",
            ]) + "\n");
        Cli.SetEnv("AW_GH", script);
    }

    /// <summary>The team config: the lab's repo, a one-step gate, and the worktree roots.</summary>
    internal async Task Team(bool enabled = true, string[]? roots = null, string[]? buildOutputs = null)
    {
        var rootList = string.Join(',', (roots ?? [Root]).Select(r => "\"" + r + "\""));
        var outputs = buildOutputs is null ? string.Empty : ",\"buildOutputs\":[" + string.Join(',', buildOutputs.Select(o => "\"" + o + "\"")) + "]";
        var json = string.Concat(
            "{\"team\":\"t\",\"repo\":\"@REPO@\",\"lanes\":[\"api\"],\"metaLane\":\"meta\",",
            "\"gate\":{\"always\":[\"repo\"],\"docsOnly\":[],\"scopes\":{\"repo\":{\"paths\":[\"**\"],\"steps\":[{\"name\":\"ok\",\"run\":[\"sh\",\"-c\",\"echo ok\"]}]}}},",
            "\"worktrees\":{\"roots\":[@ROOTS@],\"enabled\":@ENABLED@@OUTPUTS@}}")
            .Replace("@REPO@", Repo, StringComparison.Ordinal).Replace("@ROOTS@", rootList, StringComparison.Ordinal)
            .Replace("@ENABLED@", enabled ? "true" : "false", StringComparison.Ordinal).Replace("@OUTPUTS@", outputs, StringComparison.Ordinal);
        await File.WriteAllTextAsync(Cli.File("team.json"), json, TestContext.Current.CancellationToken);
        Cli.SetEnv("AW_META_CONFIG", Cli.File("team.json"));
    }

    public void Dispose()
    {
        StopSleepers();
        Cli.Dispose();
    }
}
