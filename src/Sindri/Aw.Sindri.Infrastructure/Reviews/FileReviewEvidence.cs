using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Infrastructure.Reviews;

/// <summary>
/// The evidence panel's disk lookup (PR review page spec §6.2 table, "evidence"): the ui-evidence runs under <c language="text">$AW_STATE_DIR/ui-evidence/runs/&lt;id&gt;/summary.json</c> (newest <see cref="MaxRuns"/> by folder time).
/// A run whose <c language="text">appBuild</c> is the head (or a prefix of it, 7 characters or more) matches by sha; one whose <c language="text">pr</c> names the PR number matches by that. Verification and proof packs
/// carry no head sha or PR number on disk yet (measured in Ph5c), so only ui-evidence runs are looked up; a run's folder that can't be read is left out.
/// </summary>
/// <param name="environment">Reads AW_STATE_DIR.</param>
internal sealed partial class FileReviewEvidence(Func<string, string?> environment) : IReviewEvidence
{
    /// <summary>The most run folders read for one PR page.</summary>
    internal const int MaxRuns = 200;

    private const int MaxSummaryBytes = 1024 * 1024;
    private const int MinShaChars = 7;

    public ValueTask<Result<IReadOnlyList<ReviewPageEvidenceItem>>> Find(ReviewPullRef pull, string headSha, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pull);
        ArgumentNullException.ThrowIfNull(headSha);
        var runs = System.IO.Path.Combine(SettingsFromEnvironment.StateRoot(environment), "ui-evidence", "runs");
        if (!Directory.Exists(runs))
        {
            return ValueTask.FromResult(Results.Success<IReadOnlyList<ReviewPageEvidenceItem>>([]));
        }

        List<ReviewPageEvidenceItem> items = [];
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(runs).OrderByDescending(Directory.GetLastWriteTimeUtc).Take(MaxRuns))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Read(dir, pull, headSha) is { } item)
                {
                    items.Add(item);
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return ValueTask.FromResult<Result<IReadOnlyList<ReviewPageEvidenceItem>>>(SindriErrors.ServiceFailed.With(runs + ": " + e.Message));
        }

        return ValueTask.FromResult(Results.Success<IReadOnlyList<ReviewPageEvidenceItem>>([.. items.OrderBy(static i => i.MatchedBy == "sha" ? 0 : 1).ThenByDescending(static i => i.At)]));
    }

    private static ReviewPageEvidenceItem? Read(string dir, ReviewPullRef pull, string headSha)
    {
        var path = System.IO.Path.Combine(dir, "summary.json");
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaxSummaryBytes)
            {
                return null;
            }

            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            var root = document.RootElement;
            var build = Text(root, "appBuild");
            var by = build is { Length: >= MinShaChars } && headSha.StartsWith(build, StringComparison.OrdinalIgnoreCase) ? "sha"
                : NumberOf(Text(root, "pr")) == pull.Number ? "prNumber" : null;
            if (by is null)
            {
                return null;
            }

            var finished = Text(root, "finishedAt") is { } at && DateTimeOffset.TryParse(at, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) ? parsed : new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero);
            return new ReviewPageEvidenceItem("uiEvidence", Text(root, "route") ?? System.IO.Path.GetFileName(dir), System.IO.Path.Combine("ui-evidence", "runs", System.IO.Path.GetFileName(dir), "summary.json"), Url: null, by, finished);
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement root, string name) => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    // why: planning.pr is whatever the script author wrote ("2071", "acme/api#2071", a PR link); the number is its last run of digits
    private static int? NumberOf(string? pr) => pr is not null && LastNumber.Match(pr) is { Success: true } m && int.TryParse(m.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;

    [GeneratedRegex("[0-9]+(?=[^0-9]*$)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LastNumber { get; }
}
