using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Reviews;

/// <summary>The PR page's diff (spec §6.2, §6.3, §7.1): the files grouped by the settings' globs (else the first two folders), groups folded when every file is collapsed, and a note for each file the page holds back.</summary>
internal static class ReviewPageDiffs
{
    private const string CappedText = "GitHub lists the first 3,000 files; open the rest on GitHub";
    private const int FileCap = 3000;
    private const int MaxPatchChars = 1024 * 1024;
    private const string Other = "Other";

    internal static ReviewPageDiff Build(ReviewPageFacts f)
    {
        if (!f.Files.Ok)
        {
            return new ReviewPageDiff("error", "Couldn't read the diff: " + f.Files.Error, 0, Capped: false, CappedText: null, []);
        }

        var files = f.Files.Value.Files;
        if (files.Count == 0)
        {
            return new ReviewPageDiff("empty", "This PR changes no files", 0, Capped: false, CappedText: null, []);
        }

        var settings = f.Settings.Resolved;
        var named = files.Select(x => (Group: GroupOf(settings.Groups, x.Path), File: x)).ToList();
        var order = settings.Groups.Count > 0 ? settings.Groups.Select(static g => g.Name).Append(Other) : named.Select(static n => n.Group);
        var groups = order.Distinct(StringComparer.Ordinal).Select(name =>
        {
            var inGroup = named.Where(n => n.Group == name).Select(static n => n.File).ToList();
            return (Name: name, Files: inGroup);
        }).Where(static g => g.Files.Count > 0).Select(g => new ReviewPageGroup(g.Name, g.Files.TrueForAll(x => settings.Collapse.Any(c => ScopeGlobs.Allows([c], x.Path))), [.. g.Files.Select(x => File(x, f.Pr.Url))])).ToList();
        var capped = f.Files.Value.CapReached;
        return new ReviewPageDiff("ready", Text: null, capped ? FileCap : files.Count, capped, capped ? CappedText : null, groups);
    }

    private static string GroupOf(IReadOnlyList<PrReviewGroup> groups, string path)
    {
        if (groups.Count > 0)
        {
            return groups.FirstOrDefault(g => g.Globs.Any(glob => ScopeGlobs.Allows([glob], path)))?.Name ?? Other;
        }

        var directories = path.Split('/')[..^1];
        return directories.Length == 0 ? Other : string.Join('/', directories.Take(2));
    }

    private static ReviewPageFile File(PullFile file, string prUrl)
    {
        var status = file.Status switch
        {
            PullFileStatus.Added => "added",
            PullFileStatus.Modified => "modified",
            PullFileStatus.Removed => "deleted",
            PullFileStatus.Renamed => "renamed",
            PullFileStatus.Copied => "copied",
            PullFileStatus.Changed => "changed",
            PullFileStatus.Unchanged => "changed",
            _ => "changed",
        };
        var similarity = file.Similarity;
        if (similarity is null && file.Status == PullFileStatus.Renamed && file.Additions == 0 && file.Deletions == 0)
        {
            similarity = 100;   // why: a rename with no content change is the same file
        }

        var (patch, omitted, note) = Held(file, similarity);
        var url = prUrl + "/files#diff-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(file.Path)));
        return new ReviewPageFile(file.Path, status, file.PreviousPath, similarity, file.Additions, file.Deletions, patch, omitted, note, url);
    }

    private static (string? Patch, string? Omitted, string? Note) Held(PullFile file, int? similarity)
    {
        if (file.Status == PullFileStatus.Removed)
        {
            return (file.Patch, "deleted", $"Deleted file: {Plural(file.Deletions, "line", "lines")} removed. Contents hidden.");
        }

        if (file.Patch is null)
        {
            return file.Additions + file.Deletions == 0
                ? (null, "binary", "Binary file, not shown here.")
                : (null, "forgeOmitted", "GitHub left this diff out; open the file on GitHub.");
        }

        if (file.Patch.Length > MaxPatchChars)
        {
            return (null, "tooLarge", "Too large to show here");
        }

        var renamed = file.Status == PullFileStatus.Renamed && file.PreviousPath is not null
            ? $"Renamed from {file.PreviousPath}" + (similarity is { } s ? string.Create(CultureInfo.InvariantCulture, $", {s}% the same") : string.Empty)
            : null;
        return (file.Patch, null, renamed);
    }

    private static string Plural(int n, string one, string many) => string.Create(CultureInfo.InvariantCulture, $"{n} ") + (n == 1 ? one : many);
}
