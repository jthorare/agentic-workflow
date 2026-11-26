using Aw.Sindri.Ui.Api.Projects;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>The typed sources of a project (#4499) as the lines of a form and the chips of a sheet: one branch per line as owner/name:branch, a trailing * for a prefix, one item id per line. The rules are the CLI's: at most 20 of each, and the server checks the rest.</summary>
internal static class ProjectSourceText
{
    /// <summary>What a field read as: the values, or the sentence that says what is wrong.</summary>
    /// <typeparam name="T">The kind of value.</typeparam>
    /// <param name="Value">The values; empty when there is a problem.</param>
    /// <param name="Problem">The sentence for the form, or null when the field is fine.</param>
    internal readonly record struct Reading<T>(IReadOnlyList<T> Value, string? Problem);

    private const int Max = 20;
    private static readonly System.Buffers.SearchValues<char> _hex = System.Buffers.SearchValues.Create("0123456789abcdef");

    /// <summary>The lines of the branches field, and the sheet's chips, for a card's branches.</summary>
    /// <param name="branches">The card's branches, or null.</param>
    /// <returns>One line per branch.</returns>
    internal static IReadOnlyList<string> Lines(IReadOnlyList<ProjectBranchView>? branches) =>
        [.. (branches ?? []).Select(static b => b.Repo + ":" + b.Name + (b.Prefix ? "*" : string.Empty))];

    /// <summary>Both fields read together.</summary>
    /// <param name="Branches">The branches, or null when the field is empty.</param>
    /// <param name="Items">The item ids, or null when the field is empty.</param>
    /// <param name="Problem">The first sentence that says what is wrong, or null.</param>
    internal readonly record struct Typed(IReadOnlyList<ProjectBranchView>? Branches, IReadOnlyList<string>? Items, string? Problem);

    /// <summary>Reads both fields for a new project: an empty field is null (nothing sent).</summary>
    /// <param name="branchesText">The branches field.</param>
    /// <param name="itemsText">The items field.</param>
    /// <returns>The lists, or the problem.</returns>
    internal static Typed Read(string branchesText, string itemsText)
    {
        var (branches, items) = (Branches(branchesText), Items(itemsText));
        return new Typed(branches.Value.Count > 0 ? branches.Value : null, items.Value.Count > 0 ? items.Value : null, branches.Problem ?? items.Problem);
    }

    /// <summary>The chips of a card: its branch lines, then its item ids.</summary>
    /// <param name="branches">The card's branches, or null.</param>
    /// <param name="items">The card's item ids, or null.</param>
    /// <returns>One chip text each.</returns>
    internal static IReadOnlyList<string> Chips(IReadOnlyList<ProjectBranchView>? branches, IReadOnlyList<string>? items) => [.. Lines(branches), .. items ?? []];

    /// <summary>Reads the branches field.</summary>
    /// <param name="text">The field, one branch per line.</param>
    /// <returns>The branches (none when the field is empty), or the sentence that says what is wrong.</returns>
    internal static Reading<ProjectBranchView> Branches(string text)
    {
        var lines = Split(text);
        if (lines.Length > Max)
        {
            return Bad<ProjectBranchView>(Labels.ProjectSourcesTooMany(lines.Length));
        }

        var branches = new List<ProjectBranchView>();
        foreach (var line in lines)
        {
            if (One(line) is not { } branch)
            {
                return Bad<ProjectBranchView>(Labels.ProjectBranchLineBad(line));
            }

            branches.Add(branch);
        }

        return new Reading<ProjectBranchView>(branches, Problem: null);
    }

    /// <summary>Reads the items field.</summary>
    /// <param name="text">The field, one item id per line.</param>
    /// <returns>The ids (none when the field is empty), or the sentence that says what is wrong.</returns>
    internal static Reading<string> Items(string text)
    {
        var lines = Split(text);
        return lines.Length > Max ? Bad<string>(Labels.ProjectSourcesTooMany(lines.Length))
            : lines.FirstOrDefault(static l => !IsItemId(l)) is { } bad ? Bad<string>(Labels.ProjectItemLineBad(bad))
            : new Reading<string>(lines, Problem: null);
    }

    private static string[] Split(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // a repo has no ':' and neither has a branch name, so the first ':' splits them
    private static ProjectBranchView? One(string line)
    {
        var colon = line.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0 || line[..colon].Split('/') is not [{ Length: > 0 }, { Length: > 0 }])
        {
            return null;
        }

        var name = line[(colon + 1)..];
        var prefix = name.EndsWith('*');
        name = prefix ? name[..^1] : name;
        return name.Length == 0 || name.Contains('*', StringComparison.Ordinal) ? null : new ProjectBranchView(line[..colon], name, prefix);
    }

    private static bool IsItemId(string value) =>
        value.Length == 36 && value.StartsWith("ITM-", StringComparison.Ordinal) && value.AsSpan(4).IndexOfAnyExcept(_hex) < 0;

    private static Reading<T> Bad<T>(string sentence) => new([], sentence);
}
