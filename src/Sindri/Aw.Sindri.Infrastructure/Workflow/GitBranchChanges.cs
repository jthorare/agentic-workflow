using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary><see cref="IBranchChanges"/> through git: the name-status of the diff, its zero-context patch for the added lines, and the log for the commits.</summary>
internal sealed class GitBranchChanges : IBranchChanges
{
    private const char FieldSeparator = '\u001f';

    public async ValueTask<Result<BranchChanges>> Between(string worktree, string baseSha, string head, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(worktree);
        ArgumentNullException.ThrowIfNull(baseSha);
        ArgumentNullException.ThrowIfNull(head);
        var names = await Git(worktree, ["diff", "--name-status", "--no-renames", "--no-color", baseSha, head], cancellationToken).ConfigureAwait(false);
        var patch = await Git(worktree, ["diff", "-U0", "--no-renames", "--no-color", "--no-ext-diff", baseSha, head], cancellationToken).ConfigureAwait(false);
        var log = await Git(worktree, ["log", "--reverse", $"--format=%H{FieldSeparator}%P{FieldSeparator}%s", $"{baseSha}..{head}"], cancellationToken).ConfigureAwait(false);
        return names.IsError || patch.IsError || log.IsError
            ? (names.IsError ? names.Error : patch.IsError ? patch.Error : log.Error)
            : new BranchChanges(Files(names.Value), AddedLines(patch.Value), Commits(log.Value));
    }

    private static async ValueTask<Result<string>> Git(string worktree, string[] args, CancellationToken cancellationToken)
    {
        var run = await GitProcess.Run(worktree, ["-c", "core.quotepath=false", .. args], cancellationToken).ConfigureAwait(false);
        return run.Exit == 0 ? run.Output : WorkflowErrors.EffectFailed.With($"git {args[0]} failed in {worktree}: {run.Error.Trim()}");
    }

    private static List<ChangedFile> Files(string nameStatus) =>
        [.. nameStatus.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(static line => line.Split('\t', 2))
            .Where(static f => f.Length == 2)
            .Select(static f => new ChangedFile(f[1], f[0][0] switch { 'A' => ChangeKind.Added, 'D' => ChangeKind.Deleted, _ => ChangeKind.Modified }))
            .OrderBy(static f => f.Path, StringComparer.Ordinal)];

    // The '+' lines of a zero-context patch: each hunk header '@@ -a,b +c,d @@' says the first added line is line c of the new file.
    private static List<AddedLine> AddedLines(string patch)
    {
        var lines = new List<AddedLine>();
        string? path = null;
        var next = 0;
        var previous = string.Empty;
        foreach (var line in patch.Split('\n'))
        {
            if (line.StartsWith("+++ ", StringComparison.Ordinal) && previous.StartsWith("--- ", StringComparison.Ordinal))
            {
                path = line.StartsWith("+++ b/", StringComparison.Ordinal) ? line[6..].TrimEnd('\t', '\r') : null;
            }
            else if (line.StartsWith("@@ ", StringComparison.Ordinal))
            {
                next = NewStart(line);
            }
            else if (path is not null && line.StartsWith('+'))
            {
                lines.Add(new AddedLine(path, next++, line[1..].TrimEnd('\r')));
            }

            previous = line;
        }

        return lines;
    }

    private static int NewStart(string hunk)
    {
        var plus = hunk.IndexOf('+', StringComparison.Ordinal);
        var end = hunk.IndexOfAny([',', ' '], plus);
        return plus > 0 && end > plus && int.TryParse(hunk.AsSpan(plus + 1, end - plus - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var start) ? start : 0;
    }

    private static List<ChangedCommit> Commits(string log) =>
        [.. log.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(static line => line.Split(FieldSeparator, 3))
            .Where(static f => f.Length == 3)
            .Select(static f => new ChangedCommit(f[0], f[2], f[1].Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 1))];
}
