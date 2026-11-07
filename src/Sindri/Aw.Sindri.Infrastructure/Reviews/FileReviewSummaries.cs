using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Infrastructure.Reviews;

/// <summary>
/// The per-head summary cache on disk (PR review page spec §6.2 "Cache"): <c language="text">&lt;home&gt;/reviews/cache/&lt;owner&gt;/&lt;repo&gt;/&lt;number&gt;/&lt;sha&gt;/summary.json</c>, folders 0700 and files 0600.
/// A name that fails GitHub's rules never builds a path. A file that can't be read or parsed is a miss, since the list rebuilds it.
/// </summary>
/// <param name="home">Sindri's state folder.</param>
internal sealed class FileReviewSummaries(ISindriHome home) : IReviewSummaries
{
    private const string FileName = "summary.json";

    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(ReviewSummaryJsonContext.Default);

    public ValueTask<Result<ReviewSummary?>> Read(ReviewPullRef pull, string sha, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pull);
        var folder = HeadFolder(pull, sha);
        return ValueTask.FromResult(folder.IsError ? folder.Error : Results.Success(Load(Path.Combine(folder.Value, FileName))));
    }

    public ValueTask<Result<ReviewSummary?>> Newest(ReviewPullRef pull, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pull);
        if (!pull.IsValid)
        {
            return ValueTask.FromResult<Result<ReviewSummary?>>(BadName(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{pull.Owner}/{pull.Repo}#{pull.Number}")));
        }

        var folder = PullFolder(pull);
        var found = Directory.Exists(folder)
            ? Directory.EnumerateDirectories(folder).Where(static d => ReviewPullRef.IsSha(Path.GetFileName(d))).Select(static d => Load(Path.Combine(d, FileName))).OfType<ReviewSummary>().ToList()
            : [];
        var newest = found.MaxBy(static s => s.At);
        return ValueTask.FromResult(Results.Success(newest));
    }

    public ValueTask<Result<Unit>> Write(ReviewPullRef pull, ReviewSummary summary, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pull);
        ArgumentNullException.ThrowIfNull(summary);
        var folder = HeadFolder(pull, summary.Sha);
        if (folder.IsError)
        {
            return ValueTask.FromResult<Result<Unit>>(folder.Error);
        }

        try
        {
            EnsureFolders(folder.Value);
            OwnerOnly.WriteAtomically(Path.Combine(folder.Value, FileName), AwJson.Serialize(summary, _json));
            return ValueTask.FromResult(Results.Success(Unit.Value));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return ValueTask.FromResult<Result<Unit>>(SindriErrors.ServiceFailed.With("the review cache can't be written: " + e.Message));
        }
    }

    private Result<string> HeadFolder(ReviewPullRef pull, string sha) =>
        pull.IsValid && ReviewPullRef.IsSha(sha) ? Path.Combine(PullFolder(pull), sha.ToLowerInvariant()) : BadName(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{pull.Owner}/{pull.Repo}#{pull.Number}@{sha}"));

    private string PullFolder(ReviewPullRef pull) =>
        Path.Combine(home.Path, "reviews", "cache", pull.Owner, pull.Repo, pull.Number.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static Error BadName(string what) => SindriErrors.InvalidInput.With("not a GitHub pull request name: " + what);

    private static ReviewSummary? Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var parsed = AwJson.Deserialize<ReviewSummary>(File.ReadAllBytes(path), _json);
            return parsed.IsError ? null : parsed.Value;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // why: every folder from the cache root down is made 0700, not only the last, since OwnerOnly.EnsureDirectory leaves parents it creates to the umask
    private void EnsureFolders(string head)
    {
        var root = Path.Combine(home.Path, "reviews");
        var relative = Path.GetRelativePath(root, head).Split(Path.DirectorySeparatorChar);
        var current = root;
        OwnerOnly.EnsureDirectory(current);
        foreach (var part in relative)
        {
            current = Path.Combine(current, part);
            OwnerOnly.EnsureDirectory(current);
        }
    }
}
