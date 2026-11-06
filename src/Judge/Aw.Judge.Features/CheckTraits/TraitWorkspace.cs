using Aw.Judge.Core.Images;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Traits;

namespace Aw.Judge.Features.CheckTraits;

/// <summary>
/// Works out what a check asks Jev, without asking: the repos (<see cref="TraitRepos"/>), their traits, the changed
/// files and their excerpts, each file judged against the traits of its own repo. A plan is null when there is nothing
/// to ask; a repo passed over (no traits, nothing a trait covers, an operation in progress, see
/// <see cref="TraitSkips"/>) leaves a 'skipped' line in the log with the reason.
/// </summary>
/// <param name="git">git through the process runner.</param>
/// <param name="traits">The repo's traits.</param>
/// <param name="files">The working directory and the repo's files.</param>
/// <param name="log">The trait-check log.</param>
/// <param name="sessions">The repos each session edited files in.</param>
internal sealed class TraitWorkspace(TraitGit git, ITraitStore traits, ITraitFiles files, ITraitCheckLog log, ITraitCheckSessions sessions)
{
    private readonly TraitSkips _skips = new(git, files);
    private readonly TraitRepos _repos = new(git, files, log, sessions);

    private const int MaxSourceChars = 20_000;
    private const int MaxImages = 4;
    private const long MaxImageBytes = 4 * 1024 * 1024;
    private const string DefaultBase = "origin/main";
    private static readonly string[] _diffFlags = ["--no-color", "--no-ext-diff", "-U3", "--src-prefix=a/", "--dst-prefix=b/"];

    internal static string BaseRef(CheckTraitsRequest request) => request.BaseRef ?? DefaultBase;

    internal async ValueTask<TraitPlan?> Plan(CheckTraitsRequest request, CancellationToken cancellationToken)
    {
        var (home, repos) = await _repos.For(request, cancellationToken).ConfigureAwait(false);
        List<TraitTask> tasks = [];
        var skipped = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var repo in repos)
        {
            var (repoTasks, repoSkipped) = await PlanRepo(request, repo, cancellationToken).ConfigureAwait(false);
            tasks.AddRange(repoTasks.Select(task => task with { Root = repo.Root }));
            foreach (var (path, reason) in repoSkipped)
            {
                skipped[TraitPlan.Display(home, repo.Root, path)] = reason;
            }
        }

        return tasks.Count == 0 && skipped.Count == 0 ? null : new TraitPlan(home, tasks) { Skipped = skipped };
    }

    // One repo's tasks and its image files left out; none when the repo is passed over.
    private async ValueTask<(List<TraitTask> Tasks, Dictionary<string, string> Skipped)> PlanRepo(CheckTraitsRequest request, RepoFiles repo, CancellationToken cancellationToken)
    {
        var root = repo.Root;
        var (active, edited) = await Judged(request, repo, cancellationToken).ConfigureAwait(false);
        if (active.Count == 0)
        {
            return ([], []);
        }

        var diffs = request.Mode == TraitCheckMode.Scan
            ? await Scanned(root, request.Paths ?? [], active, cancellationToken).ConfigureAwait(false)
            : await NotGenerated(root, TraitDiff.Parse(await Changed(root, request, edited, cancellationToken).ConfigureAwait(false)), cancellationToken).ConfigureAwait(false);
        var (tasks, skipped) = await WithImages(root, edited, active, Tasks(root, diffs, active), cancellationToken).ConfigureAwait(false);
        if (tasks.Count == 0 && skipped.Count == 0)
        {
            _repos.Skipped(request, ("repo", root), "no-change-in-scope");
        }

        return (tasks, skipped);
    }

    // The repo's traits to check and its edited files that aren't generated; no traits when the repo is passed over.
    private async ValueTask<(List<Trait> Active, List<string> Edited)> Judged(CheckTraitsRequest request, RepoFiles repo, CancellationToken cancellationToken)
    {
        var root = repo.Root;
        if (request.Mode != TraitCheckMode.Scan && await _skips.OperationInProgress(root, cancellationToken).ConfigureAwait(false))
        {
            _repos.Skipped(request, ("repo", root), "operation-in-progress");
            return ([], []);
        }

        var active = await Active(request, root, cancellationToken).ConfigureAwait(false);
        var edited = request.Mode == TraitCheckMode.Edit && active.Count > 0 ? await NotGenerated(root, repo.Files, cancellationToken).ConfigureAwait(false) : [];
        var reason = active.Count == 0 ? "no-traits"
            : request.Mode == TraitCheckMode.Edit && !edited.Exists(file => active.Exists(trait => TraitGlob.InScope(trait.Paths, file))) ? "no-file-in-scope"
            : null;
        if (reason is not null)
        {
            _repos.Skipped(request, ("repo", root), reason);
            return ([], []);
        }

        return (active, edited);
    }

    private async ValueTask<List<string>> NotGenerated(string root, List<string> paths, CancellationToken cancellationToken)
    {
        var generated = await _skips.Generated(root, paths, cancellationToken).ConfigureAwait(false);
        return [.. paths.Where(path => !generated.Contains(path))];
    }

    private async ValueTask<IReadOnlyList<FileDiff>> NotGenerated(string root, IReadOnlyList<FileDiff> diffs, CancellationToken cancellationToken)
    {
        var generated = await _skips.Generated(root, [.. diffs.Select(diff => diff.Path)], cancellationToken).ConfigureAwait(false);
        return [.. diffs.Where(diff => !generated.Contains(diff.Path))];
    }

    // An edit (the only mode with edited files) also asks about its image files.
    private async ValueTask<(List<TraitTask> Tasks, Dictionary<string, string> Skipped)> WithImages(string root, List<string> edited, List<Trait> active, List<TraitTask> tasks, CancellationToken cancellationToken)
    {
        var skipped = new Dictionary<string, string>(StringComparer.Ordinal);
        var images = await ImageTasks(root, edited, active, skipped, cancellationToken).ConfigureAwait(false);

        // A file named .png that isn't an image (a text file) is image-not-sent only; its text is not judged either.
        _ = tasks.RemoveAll(task => skipped.ContainsKey(task.Source.Path));
        tasks.AddRange(images);
        return (tasks, skipped);
    }

    // An edited PNG or JPEG is read as an image (best effort): inside the checkout, the first 4 files and 4 MiB; the rest are skipped, never judged on their names.
    private async ValueTask<List<TraitTask>> ImageTasks(string root, List<string> edited, List<Trait> active, Dictionary<string, string> skipped, CancellationToken cancellationToken)
    {
        List<TraitTask> tasks = [];
        long total = 0;
        var sent = 0;
        foreach (var file in edited.Where(IsImage).Distinct(StringComparer.Ordinal))
        {
            var applicable = active.Where(trait => TraitGlob.InScope(trait.Paths, file)).ToList();
            if (applicable.Count == 0)
            {
                continue;
            }

            var (image, why) = await Admit(root, file, (sent, total), cancellationToken).ConfigureAwait(false);
            if (image is null)
            {
                skipped[file] = CheckTraitsResponse.ImageNotSent;
                log.Append(new System.Text.Json.Nodes.JsonObject { ["event"] = "image-not-sent", ["repo"] = root, ["path"] = file, ["reason"] = why });
                continue;
            }

            total += image.Bytes.Length;
            sent++;
            var source = new DiffSource(file, 1, 1, "(an image file: its picture is attached)");
            tasks.AddRange(TraitQuestions.Batches(applicable).Select(batch => new TraitTask(source, batch) { Images = [image] }));
        }

        return tasks;
    }

    // The size comes from file metadata: a file over what is left of the 4 files and 4 MiB is skipped without being read.
    private async ValueTask<(ImageAttachment? Image, string Why)> Admit(string root, string file, (int Sent, long Total) so, CancellationToken cancellationToken)
    {
        var path = Path.Combine(root, file);
        if (so.Sent >= MaxImages || files.FileSize(path) is not { } size || so.Total + size > MaxImageBytes)
        {
            return (null, "past the 4 files and 4 MiB an edit sends");
        }

        var read = await files.ReadImage(path, root, cancellationToken).ConfigureAwait(false);
        return read.Images is [var image] && so.Total + image.Bytes.Length <= MaxImageBytes ? (image, "") : (null, read.Problem ?? "not an image Jev can be sent");
    }

    private static bool IsImage(string file) =>
        file.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase);

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
    // Files no active trait covers, and generated files, are never read.
    private async ValueTask<IReadOnlyList<FileDiff>> Scanned(string root, IReadOnlyList<string> paths, List<Trait> active, CancellationToken cancellationToken)
    {
        var (_, listed) = await git.Run(["ls-files", "-z", "--", .. paths], root, cancellationToken).ConfigureAwait(false);
        List<string> covered = [.. listed.Split('\0', StringSplitOptions.RemoveEmptyEntries).Where(file => active.Exists(trait => TraitGlob.InScope(trait.Paths, file)))];
        List<FileDiff> scanned = [];
        foreach (var file in await NotGenerated(root, covered, cancellationToken).ConfigureAwait(false))
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
