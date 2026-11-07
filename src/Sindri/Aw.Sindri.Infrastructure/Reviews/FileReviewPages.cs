using System.Globalization;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Infrastructure.Reviews;

/// <summary>
/// The page snapshot per head on disk (PR review page spec §6.2 "Cache"): <c language="text">&lt;home&gt;/reviews/cache/&lt;owner&gt;/&lt;repo&gt;/&lt;number&gt;/&lt;sha&gt;/page.json</c>, folders 0700 and files 0600, beside the head's summary.json.
/// A name that fails GitHub's rules never builds a path. The newest <see cref="KeepHeads"/> heads of a PR are kept. A file that can't be read or parsed is a miss.
/// </summary>
/// <param name="home">Sindri's state folder.</param>
/// <param name="clock">Dates a page when it is written.</param>
internal sealed class FileReviewPages(ISindriHome home, TimeProvider clock) : IReviewPages
{
    /// <summary>How many heads of one PR stay on disk.</summary>
    internal const int KeepHeads = 3;

    private const string FileName = "page.json";

    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(ReviewPageJsonContext.Default);

    public ValueTask<Result<CachedReviewPage?>> Newest(ReviewPullRef pull, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pull);
        if (!pull.IsValid)
        {
            return ValueTask.FromResult<Result<CachedReviewPage?>>(BadName(Name(pull)));
        }

        var folder = PullFolder(pull);
        var found = Directory.Exists(folder)
            ? Directory.EnumerateDirectories(folder).Where(static d => ReviewPullRef.IsSha(System.IO.Path.GetFileName(d))).Select(static d => Load(System.IO.Path.Combine(d, FileName))).OfType<CachedReviewPage>().ToList()
            : [];
        return ValueTask.FromResult(Results.Success(found.MaxBy(static c => c.CachedAt)));
    }

    public ValueTask<Result<Unit>> Write(ReviewPullRef pull, ReviewPageModel page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pull);
        ArgumentNullException.ThrowIfNull(page);
        if (!pull.IsValid || !ReviewPullRef.IsSha(page.Pr.Head.Sha))
        {
            return ValueTask.FromResult<Result<Unit>>(BadName(Name(pull) + "@" + page.Pr.Head.Sha));
        }

        try
        {
            var head = System.IO.Path.Combine(PullFolder(pull), page.Pr.Head.Sha.ToLowerInvariant());
            EnsureFolders(head);
            OwnerOnly.WriteAtomically(System.IO.Path.Combine(head, FileName), AwJson.Serialize(new CachedReviewPage(page, clock.GetUtcNow()), _json));
            Prune(PullFolder(pull));
            return ValueTask.FromResult(Results.Success(Unit.Value));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return ValueTask.FromResult<Result<Unit>>(SindriErrors.ServiceFailed.With("the review cache can't be written: " + e.Message));
        }
    }

    private static string Name(ReviewPullRef pull) => string.Create(CultureInfo.InvariantCulture, $"{pull.Owner}/{pull.Repo}#{pull.Number}");

    private static Error BadName(string what) => SindriErrors.InvalidInput.With("not a GitHub pull request name: " + what);

    private string PullFolder(ReviewPullRef pull) =>
        System.IO.Path.Combine(home.Path, "reviews", "cache", pull.Owner, pull.Repo, pull.Number.ToString(CultureInfo.InvariantCulture));

    private static CachedReviewPage? Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var parsed = AwJson.Deserialize<CachedReviewPage>(File.ReadAllBytes(path), _json);
            return parsed.IsError ? null : parsed.Value;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // why: the cache keeps the newest heads only (a head is as new as its page, else as its newest file), so a long-lived PR does not pile up folders
    private static void Prune(string pullFolder)
    {
        var heads = Directory.EnumerateDirectories(pullFolder).Where(static d => ReviewPullRef.IsSha(System.IO.Path.GetFileName(d)))
            .OrderByDescending(static d => Load(System.IO.Path.Combine(d, FileName))?.CachedAt ?? new DateTimeOffset(Directory.EnumerateFiles(d).Select(File.GetLastWriteTimeUtc).DefaultIfEmpty(DateTime.MinValue).Max(), TimeSpan.Zero)).ToList();
        foreach (var old in heads.Skip(KeepHeads))
        {
            Directory.Delete(old, recursive: true);
        }
    }

    // why: every folder from the cache root down is made 0700, not only the last, since OwnerOnly.EnsureDirectory leaves parents it creates to the umask
    private void EnsureFolders(string head)
    {
        var root = System.IO.Path.Combine(home.Path, "reviews");
        var relative = System.IO.Path.GetRelativePath(root, head).Split(System.IO.Path.DirectorySeparatorChar);
        var current = root;
        OwnerOnly.EnsureDirectory(current);
        foreach (var part in relative)
        {
            current = System.IO.Path.Combine(current, part);
            OwnerOnly.EnsureDirectory(current);
        }
    }
}
