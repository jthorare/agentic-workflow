using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The Files view's role lines: which files the repo's profile demotes or ignores, and the role-like ones no glob covers.</summary>
internal sealed partial class FileMapViewModel
{
    /// <summary>One line per role of the repo ("test, demoted: 41 files"); empty when the map status names no role for it.</summary>
    public IReadOnlyList<string> RoleLines { get; private set => Set(ref field, value); } = [];

    /// <summary>"2 files look like a role but match no glob", or empty when none do.</summary>
    public string UnassignedLine { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The paths of a few of those files, or empty.</summary>
    public string UnassignedExamples { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Whether there is a role line or an unassigned line to show.</summary>
    public bool HasRoles => RoleLines.Count > 0 || HasUnassigned;

    /// <summary>Whether role-like files match no glob.</summary>
    public bool HasUnassigned => UnassignedLine.Length > 0;

    // The repo's own entry of the status's role counts; no entry, or one with nothing in it, shows nothing.
    private void ShowRoles(IReadOnlyList<RepoRolesView>? roles, string repo)
    {
        var mine = roles?.FirstOrDefault(r => string.Equals(r.Repo, repo, StringComparison.Ordinal));
        RoleLines = mine is null ? [] : [.. mine.Roles.Select(r => Labels.ContextRoleLine(r.Role, r.Demoted, r.Files))];
        if (mine?.Unassigned is { Count: > 0 } unassigned)
        {
            UnassignedLine = Labels.ContextRolesUnassigned(unassigned.Count);
            UnassignedExamples = string.Join(", ", unassigned.Examples);
        }
        else
        {
            UnassignedLine = string.Empty;
            UnassignedExamples = string.Empty;
        }

        OnChanged(nameof(HasRoles));
        OnChanged(nameof(HasUnassigned));
    }
}
