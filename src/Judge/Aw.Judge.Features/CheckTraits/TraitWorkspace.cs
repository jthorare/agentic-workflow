using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Traits;

namespace Aw.Judge.Features.CheckTraits;

/// <summary>
/// Works out what a check asks Jev, without asking: the repo, its traits, the changed files and their excerpts. A plan
/// is null when there is nothing to ask (not a repo, no traits, no changed file a trait covers).
/// </summary>
/// <param name="git">git through the process runner.</param>
/// <param name="traits">The repo's traits.</param>
/// <param name="files">The working directory and the repo's files.</param>
/// <param name="log">The trait-check log.</param>
internal sealed class TraitWorkspace(TraitGit git, ITraitStore traits, ITraitFiles files, ITraitCheckLog log)
{
    private const int MaxSourceChars = 20_000;
    private const string DefaultBase = "origin/main";
    private static readonly string[] _diffFlags = ["--no-color", "--no-ext-diff", "-U3", "--src-prefix=a/", "--dst-prefix=b/"];

    internal static string BaseRef(CheckTraitsRequest request) => request.BaseRef ?? DefaultBase;

    internal async ValueTask<TraitPlan?> Plan(CheckTraitsRequest request, CancellationToken cancellationToken)
    {
        var (code, top) = await git.Run(["rev-parse", "--show-toplevel"], files.WorkingDirectory, cancellationToken).ConfigureAwait(false);
        if (code != 0)
        {
            return null;
        }

        var root = top.Trim();
        var active = await Active(request, root, cancellationToken).ConfigureAwait(false);
        var edited = request.Mode == TraitCheckMode.Edit ? Relative(root, request.Files) : [];
        if (active.Count == 0 || (request.Mode == TraitCheckMode.Edit && !edited.Exists(file => active.Exists(trait => TraitGlob.InScope(trait.Paths, file)))))
        {
            return null;
        }

        var diffs = request.Mode == TraitCheckMode.Scan
            ? await Scanned(root, request.Paths ?? [], active, cancellationToken).ConfigureAwait(false)
            : TraitDiff.Parse(await Changed(root, request, edited, cancellationToken).ConfigureAwait(false));
        var tasks = Tasks(root, diffs, active);
        return tasks.Count == 0 ? null : new TraitPlan(root, tasks);
    }

    // The traits to check: the repo's, limited to the ones named; a file that isn't a trait is logged.
    private async ValueTask<List<Trait>> Active(CheckTraitsRequest request, string root, CancellationToken cancellationToken)
    {
        var (_, common) = await git.Run(["rev-parse", "--path-format=absolute", "--git-common-dir"], root, cancellationToken).ConfigureAwait(false);
        var loaded = traits.Load(root, Path.GetFileName(Path.GetDirectoryName(common.Trim()) ?? ""));
        foreach (var problem in loaded.Problems)
        {
            log.Append(new System.Text.Json.Nodes.JsonObject { ["event"] = "trait-parse-error", ["file"] = problem.File, ["error"] = problem.Error });
        }

        var only = request.Traits ?? [];
        return [.. loaded.Traits.Where(trait => only.Count == 0 || only.Contains(trait.Name, StringComparer.Ordinal))];
    }

    // The edited files as repo-relative paths; a path outside the repo is dropped.
    private List<string> Relative(string root, IReadOnlyList<string> edited) =>
        [.. edited
            .Select(file => Path.GetRelativePath(root, Path.GetFullPath(file, files.WorkingDirectory)).Replace('\\', '/'))
            .Where(file => file != "." && file != ".." && !file.StartsWith("../", StringComparison.Ordinal))];

    // Per file, the excerpts (a file's hunks sized for a request) against the traits that apply to the file.
    private List<TraitTask> Tasks(string root, IReadOnlyList<FileDiff> diffs, List<Trait> active)
    {
        List<TraitTask> tasks = [];
        foreach (var file in diffs)
        {
            var head = files.ReadText(Path.Combine(root, file.Path)) ?? "";
            var ignored = TraitDiff.Ignored(head.Length > 4096 ? head[..4096] : head);
            var applicable = active.Where(trait => TraitGlob.InScope(trait.Paths, file.Path) && !ignored.Contains(trait.Name)).ToList();
            if (applicable.Count > 0)
            {
                var batches = TraitQuestions.Batches(applicable);
                tasks.AddRange(TraitDiff.Chunk(file, MaxSourceChars).SelectMany(source => batches.Select(batch => new TraitTask(source, batch))));
            }
        }

        return tasks;
    }

    // Scan: every tracked file under the pathspecs, as one all-added hunk, so the same question judges existing code.
    // Files no active trait covers are never read.
    private async ValueTask<IReadOnlyList<FileDiff>> Scanned(string root, IReadOnlyList<string> paths, List<Trait> active, CancellationToken cancellationToken)
    {
        var (_, listed) = await git.Run(["ls-files", "-z", "--", .. paths], root, cancellationToken).ConfigureAwait(false);
        List<FileDiff> scanned = [];
        foreach (var file in listed.Split('\0', StringSplitOptions.RemoveEmptyEntries).Where(file => active.Exists(trait => TraitGlob.InScope(trait.Paths, file))))
        {
            if (files.ReadText(Path.Combine(root, file)) is { } text)
            {
                scanned.Add(TraitDiff.Whole(file, text));
            }
        }

        return scanned;
    }

    // Edit: the files against HEAD. Stop: the branch against its merge base with the base ref. Files git doesn't track yet
    // are diffed against /dev/null, as new files.
    private async ValueTask<string> Changed(string root, CheckTraitsRequest request, List<string> edited, CancellationToken cancellationToken)
    {
        var against = "HEAD";
        if (request.Mode == TraitCheckMode.Stop)
        {
            var (code, mergeBase) = await git.Run(["merge-base", "HEAD", BaseRef(request)], root, cancellationToken).ConfigureAwait(false);
            against = code == 0 ? mergeBase.Trim() : against;
        }

        string[] scope = ["--", .. edited];
        var (_, tracked) = await git.Run(["diff", .. _diffFlags, against, .. scope], root, cancellationToken).ConfigureAwait(false);
        var (_, untracked) = await git.Run(["ls-files", "--others", "--exclude-standard", "-z", .. scope], root, cancellationToken).ConfigureAwait(false);
        List<string> diffs = [tracked];
        foreach (var file in untracked.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var (_, added) = await git.Run(["diff", "--no-index", .. _diffFlags, "--", "/dev/null", file], root, cancellationToken).ConfigureAwait(false);
            diffs.Add(added);
        }

        return string.Join('\n', diffs);
    }
}
