using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Projects.Sources;

namespace Aw.Sindri.Core.Projects;

/// <summary>
/// The rules for a project's typed sources (#4499): at most 20 branches (or prefixes) and 20 work items, a branch name git's own check-ref-format would take, a repo in the owner/name shape, an item id of the existing id shape.
/// A repo does not need a GitHub link on the project (it may be covered through Linear or the profile); a null entry, or '.' or '..' as owner or name, is refused. Every refusal is SND-PROJECT-004 naming the field ('branches' or 'items').
/// </summary>
internal static class ProjectSourceRules
{
    /// <summary>The most entries of one kind.</summary>
    internal const int Max = 20;

    private const int NameMax = 255;
    private const string Unsafe = " ~^:?*[\\";

    /// <summary>Checks both typed lists.</summary>
    /// <param name="branches">The typed branches, or null for none.</param>
    /// <param name="items">The typed item ids, or null for none.</param>
    /// <returns>The refusal, or null.</returns>
    internal static Error? Check(IReadOnlyList<BranchSource>? branches, IReadOnlyList<string>? items) =>
        BranchesError(branches) ?? ItemsError(items);

    /// <summary>The branches as stored: the repo trimmed, none twice (a repo's case does not tell two apart; the first spelling stays).</summary>
    /// <param name="branches">The typed branches, or null.</param>
    /// <returns>The cleaned list, or null.</returns>
    internal static IReadOnlyList<BranchSource>? Clean(IReadOnlyList<BranchSource>? branches) =>
        branches?.Select(static b => b with { Repo = b.Repo.Trim() }).DistinctBy(static b => (b.Repo.ToUpperInvariant(), b.Name, b.IsPrefix)).ToList();

    /// <summary>The item ids as stored: trimmed, none twice.</summary>
    /// <param name="items">The typed item ids, or null.</param>
    /// <returns>The cleaned list, or null.</returns>
    internal static IReadOnlyList<string>? Clean(IReadOnlyList<string>? items) =>
        items?.Select(static i => i.Trim()).Distinct(StringComparer.Ordinal).ToList();

    private static Error? BranchesError(IReadOnlyList<BranchSource>? branches)
    {
        if (branches is null)
        {
            return null;
        }

        if (branches.Count > Max)
        {
            return Invalid("branches", string.Create(CultureInfo.InvariantCulture, $"{branches.Count} entries (at most {Max})"));
        }

        if (branches.Any(static b => b is null || b.Repo is null || b.Name is null))
        {
            return Invalid("branches", "an entry is null");
        }

        return branches.Select(One).FirstOrDefault(static error => error is not null);
    }

    private static Error? One(BranchSource branch)
    {
        var repo = branch.Repo.Trim();
        return !IsRepo(repo) ? Invalid("branches", $"'{repo}' isn't a GitHub repo (owner/name)")
            : IsRef(branch.Name, branch.IsPrefix) ? null
            : Invalid("branches", $"'{branch.Name}' isn't {(branch.IsPrefix ? "the start of a branch name" : "a branch name")} git accepts (git check-ref-format --branch)");
    }

    private static Error? ItemsError(IReadOnlyList<string>? items)
    {
        if (items is null)
        {
            return null;
        }

        if (items.Count > Max)
        {
            return Invalid("items", string.Create(CultureInfo.InvariantCulture, $"{items.Count} entries (at most {Max})"));
        }

        if (items.Any(static i => i is null))
        {
            return Invalid("items", "an entry is null");
        }

        return items.Select(static i => i.Trim()).FirstOrDefault(static i => !ItemId.TryFrom(i, out _)) is { } bad
            ? Invalid("items", $"'{bad}' isn't an item id (ITM- and 32 lowercase hex digits)")
            : null;
    }

    private static bool IsRepo(string repo) =>
        repo.Split('/') is [{ Length: > 0 } owner, { Length: > 0 } name] && IsRepoPart(owner) && IsRepoPart(name);

    private static bool IsRepoPart(string part) => part is not ("." or "..") && part.All(IsRepoChar);

    private static bool IsRepoChar(char c) => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.';

    // git check-ref-format --branch, for a whole name; a prefix is checked as the start of one (it plus one more letter is a name)
    private static bool IsRef(string name, bool prefix)
    {
        var text = prefix ? name + "x" : name;
        return name.Length > 0 && text.Length <= NameMax && EdgesOk(text) && !HasBadSequence(text) && text.All(IsRefChar) && text.Split('/').All(IsRefPart);
    }

    private static bool IsRefChar(char c) => c >= ' ' && c != '\u007f' && !Unsafe.Contains(c, StringComparison.Ordinal);

    private static bool IsRefPart(string part) => part.Length > 0 && part[0] != '.' && !part.EndsWith(".lock", StringComparison.Ordinal);

    private static bool EdgesOk(string text) => text[0] is not ('-' or '/') && text[^1] is not ('/' or '.') && text != "@";

    private static bool HasBadSequence(string text) =>
        text.Contains("..", StringComparison.Ordinal) || text.Contains("@{", StringComparison.Ordinal) || text.Contains("//", StringComparison.Ordinal);

    private static Error Invalid(string field, string why) => SindriErrors.ProjectInvalid.With($"{field}: {why}");
}
