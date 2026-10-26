using System.Globalization;
using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.Kernel;

namespace Aw.BugFix.Infrastructure;

/// <summary>The git CLI on PATH.</summary>
internal sealed class GitProcess : IGit
{
    private static readonly TimeSpan _timeout = TimeSpan.FromMinutes(2);

    public Result<string> Head(string directory) => Value(directory, "rev-parse", "HEAD");

    public Result<string> Branch(string directory) => Value(directory, "rev-parse", "--abbrev-ref", "HEAD");

    public Result<bool> IsClean(string directory) =>
        Value(directory, "status", "--porcelain", "--untracked-files=no").Map(static s => s.Length == 0);

    // From the root, so the whole work tree counts (ls-files lists only below the directory it runs in).
    public Result<IReadOnlyList<string>> Untracked(string directory) =>
        Root(directory).Bind(root => Paths(root, "ls-files", "--others", "--exclude-standard", "-z"));

    public Result<string> Root(string directory) => Value(directory, "rev-parse", "--show-toplevel");

    public Result<string> RelativeDirectory(string directory) =>
        Value(directory, "rev-parse", "--show-prefix").Map(static p => p.TrimEnd('/'));

    public Result<bool> IsCommittedUnchanged(string root, string file)
    {
        var committed = Run(root, "rev-parse", "--verify", "--quiet", $"HEAD:{file}");
        if (committed.ExitCode != 0)
        {
            return false;
        }

        return Value(root, "hash-object", "--", file).Map(working => working == committed.Output.Trim());
    }

    public Result<bool> IsAncestor(string directory, string ancestor, string commit)
    {
        var run = Run(directory, "merge-base", "--is-ancestor", ancestor, commit);
        return run.ExitCode switch
        {
            0 => true,
            1 => false,
            _ => Failed(run, "merge-base"),
        };
    }

    public Result<bool> SameTree(string directory, string first, string second) =>
        Value(directory, "rev-parse", $"{first}^{{tree}}").Bind(a => Value(directory, "rev-parse", $"{second}^{{tree}}").Map(b => a == b));

    // The baseline merged with the main commit the candidate merged: the replay of that merge onto the baseline, so
    // the diff from it holds only the fix. Any trouble (no main ref, a conflict, an old git) falls back to the baseline.
    public string NetBase(string directory, string baseline, string candidate)
    {
        if (MainCommitOf(directory, candidate) is not { } mainCommit)
        {
            return baseline;
        }

        var merged = Run(directory, "merge-tree", "--write-tree", baseline, mainCommit);
        return merged.ExitCode == 0 && merged.Output.Split('\n')[0].Trim() is { Length: > 0 } tree ? tree : baseline;
    }

    // Main's commits the baseline lacks, up to the one the candidate's branch last met: what NetBase adds to the baseline.
    public Result<IReadOnlyList<MainCommit>> MainCommits(string directory, string baseline, string candidate)
    {
        if (MainCommitOf(directory, candidate) is not { } mainCommit)
        {
            return Results.Success<IReadOnlyList<MainCommit>>([]);
        }

        var run = Run(directory, "log", "--no-merges", "--reverse", "--format=%H%x1f%P%x1f%B%x1e", $"{baseline}..{mainCommit}");
        return run.ExitCode == 0
            ? Results.Success<IReadOnlyList<MainCommit>>([.. run.Output.Split('\u001e', StringSplitOptions.RemoveEmptyEntries).Select(static r => r.Trim('\n').Split('\u001f')).Where(static f => f.Length == 3 && f[1].Length > 0)
                .Select(static f => new MainCommit(f[0], f[1], f[2]))])
            : Failed(run, "log");
    }

    public Result<string> FileAt(string directory, string commit, string path)
    {
        var run = Run(directory, "show", $"{commit}:{path}");
        return run.ExitCode == 0 ? run.Output : Failed(run, "show");
    }

    // The commit of main the candidate's branch last met, or null when main can't be found.
    private static string? MainCommitOf(string directory, string candidate)
    {
        foreach (var main in new[] { "origin/HEAD", "origin/main", "main" })
        {
            if (Run(directory, "rev-parse", "--verify", "--quiet", $"{main}^{{commit}}").ExitCode != 0)
            {
                continue;
            }

            var mergeBase = Run(directory, "merge-base", candidate, main);
            return mergeBase.ExitCode == 0 && mergeBase.Output.Trim() is { Length: > 0 } mainCommit ? mainCommit : null;
        }

        return null;
    }

    // -z: raw (unquoted) paths; --no-renames: a moved test reports its old path too.
    public Result<IReadOnlyList<string>> ChangedFiles(string directory, string from, string to) =>
        Paths(directory, "diff", "--name-only", "--no-renames", "-z", from, to);

    public Result<IReadOnlyList<string>> AddedFiles(string directory, string from, string to) =>
        Paths(directory, "diff", "--name-only", "--no-renames", "--diff-filter=A", "-z", from, to);

    // Lock files and build output are noise to judge; without the full diff judge still has the stat.
    public Result<CommitDiff> Diff(string directory, string from, string to) =>
        Value(directory, "diff", "--stat", from, to).Map(stat =>
        {
            var full = Run(directory, "diff", from, to, "--", ".", ":(glob,exclude)**/package-lock.json", ":(glob,exclude)**/*.lock", ":(glob,exclude)**/pnpm-lock.yaml", ":(glob,exclude)**/packages.lock.json", ":(glob,exclude)**/dist/**");
            return new CommitDiff(stat, full.ExitCode == 0 ? full.Output : null);
        });

    private static Result<IReadOnlyList<string>> Paths(string directory, params string[] args)
    {
        var run = Run(directory, args);
        return run.ExitCode == 0
            ? Results.Success<IReadOnlyList<string>>(run.Output.Split('\0', StringSplitOptions.RemoveEmptyEntries))
            : Failed(run, args[0]);
    }

    private static Result<string> Value(string directory, params string[] args)
    {
        var run = Run(directory, args);
        return run.ExitCode == 0 ? run.Output.TrimEnd('\n') : Failed(run, args[0]);
    }

    private static ProcessResult Run(string directory, params string[] args)
    {
        var start = Processes.Start("git", ["-C", directory, "-c", "core.quotepath=off", .. args]);
        CheckEnvironment.StripGit(start.Environment);
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        return Processes.Capture(start, _timeout);
    }

    private static Error Failed(ProcessResult run, string command)
    {
        var reason = run.Error.Trim().Split('\n')[0];
        return BugFixErrors.GitFailed.With($"git {command} failed: {(reason.Length > 0 ? reason : string.Create(CultureInfo.InvariantCulture, $"exit {run.ExitCode}"))}");
    }
}
