using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Projects.Cards;
using Aw.Sindri.Core.Projects.TruthSources;

namespace Aw.Sindri.Host.Commands;

/// <summary>The typed-source flags of 'project add' and 'project edit' (#4499): --branch, --branch-prefix, --item, --clear-branches and --clear-items, what they send, and how 'project show' prints them.</summary>
internal static class ProjectVerbSources
{
    /// <summary>Whether the flag is one of the five, on a verb that takes it (the clear flags are edit only).</summary>
    /// <param name="flag">The flag.</param>
    /// <param name="edit">Whether the verb is 'project edit'.</param>
    /// <param name="add">Whether the verb is 'project add'.</param>
    /// <returns>True when this class reads it.</returns>
    internal static bool Takes(string flag, bool edit, bool add) => flag switch
    {
        "--branch" or "--branch-prefix" or "--item" => edit || add,
        "--clear-branches" or "--clear-items" => edit,
        _ => false,
    };

    /// <summary>Reads the flag at <paramref name="i"/>: --branch and --branch-prefix take owner/name:branch (a repo has no ':' and neither has a branch name), --item an item id; the server checks the rest.</summary>
    /// <param name="parsed">The arguments so far.</param>
    /// <param name="rest">The words after the verb.</param>
    /// <param name="i">The flag's position.</param>
    /// <returns>How many extra words it took, or the usage error.</returns>
    internal static Result<int> Read(ProjectVerbArguments.Parsed parsed, string[] rest, int i)
    {
        var flag = rest[i];
        switch (flag)
        {
            case "--clear-branches":
                parsed.ClearBranches = true;
                return 0;
            case "--clear-items":
                parsed.ClearItems = true;
                return 0;
            default:
                return i + 1 < rest.Length ? Value(parsed, flag, rest[i + 1]) : SindriErrors.InvalidInput.With($"{flag} needs a value: {ProjectVerbArguments.Usage}");
        }
    }

    /// <summary>The error for a clear flag given with its list flag, or null.</summary>
    /// <param name="parsed">The arguments.</param>
    /// <returns>What was mixed, or null.</returns>
    internal static string? ClearsAndSets(ProjectVerbArguments.Parsed parsed) =>
        parsed.ClearBranches && parsed.Branches.Count > 0 ? "--clear-branches with --branch or --branch-prefix"
            : parsed.ClearItems && parsed.Items.Count > 0 ? "--clear-items with --item"
            : null;

    /// <summary>The branches a write sends: an empty list for a clear flag, the typed ones, or null (the project keeps its own).</summary>
    /// <param name="command">The arguments.</param>
    /// <returns>The list, or null.</returns>
    internal static List<BranchSource>? Branches(ProjectVerbArguments.Parsed command) => command.ClearBranches ? [] : command.Branches.Count == 0 ? null : command.Branches;

    /// <summary>The item ids a write sends, as <see cref="Branches"/> does.</summary>
    /// <param name="command">The arguments.</param>
    /// <returns>The list, or null.</returns>
    internal static List<string>? Items(ProjectVerbArguments.Parsed command) => command.ClearItems ? [] : command.Items.Count == 0 ? null : command.Items;

    /// <summary>Prints a card's typed sources, one line each.</summary>
    /// <param name="text">The output so far.</param>
    /// <param name="card">The card.</param>
    internal static void Render(StringBuilder text, ProjectCard card)
    {
        foreach (var branch in card.Branches ?? [])
        {
            _ = text.Append(branch.IsPrefix ? "  branch prefix: " : "  branch: ").Append(branch.Repo).Append(' ').AppendLine(branch.Name);
        }

        foreach (var item in card.Items ?? [])
        {
            _ = text.Append("  item: ").AppendLine(item);
        }
    }

    private static Result<int> Value(ProjectVerbArguments.Parsed parsed, string flag, string value)
    {
        if (flag == "--item")
        {
            parsed.Items.Add(value);
            return 1;
        }

        var colon = value.IndexOf(':', StringComparison.Ordinal);
        var (repo, name) = colon < 0 ? (string.Empty, string.Empty) : (value[..colon], value[(colon + 1)..]);
        if (repo.Split('/') is not [{ Length: > 0 }, { Length: > 0 }] || name.Length == 0)
        {
            return SindriErrors.InvalidInput.With($"{flag} takes owner/name:branch (for example acme/api:feat/login), not '{value}'");
        }

        parsed.Branches.Add(new BranchSource(repo, name, IsPrefix: flag == "--branch-prefix"));
        return 1;
    }
}
