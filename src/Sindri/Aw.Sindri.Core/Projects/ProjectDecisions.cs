using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Projects;

/// <summary>The pure decisions of the project commands (projects page spec §5.1, §5.3, §8).</summary>
public static class ProjectDecisions
{
    /// <summary>Attaches a Linear project or adds a hand-made one.</summary>
    /// <param name="command">The command's id.</param>
    /// <param name="id">The new project's id.</param>
    /// <param name="draft">What the owner typed.</param>
    /// <param name="attached">The projects attached now.</param>
    /// <param name="led">Whether the owner leads the draft's Linear project already (it is on the page without being attached).</param>
    /// <returns>The event to append, SND-PROJECT-004 naming a bad field, or SND-PROJECT-001 for a project already on the page.</returns>
    public static Result<ProjectAttached> Attach(CommandId command, ProjectId id, ProjectDraft draft, IReadOnlyList<ProjectAttachedRow> attached, bool led) => throw new NotSupportedException();

    /// <summary>Changes an attached or hand-made project that is still at the version the owner saw.</summary>
    /// <param name="command">The command's id.</param>
    /// <param name="id">The project the owner named.</param>
    /// <param name="project">The project as the view holds it, or null when there is none.</param>
    /// <param name="edit">What to change.</param>
    /// <param name="seenVersion">The version the owner saw.</param>
    /// <returns>The event to append, SND-PROJECT-404, SND-PROJECT-409, or SND-PROJECT-004 naming a bad field.</returns>
    public static Result<ProjectChanged> Change(CommandId command, ProjectId id, ProjectAttachedRow? project, ProjectEdit edit, long seenVersion) => throw new NotSupportedException();

    /// <summary>Removes an attached or hand-made project that is still at the version the owner saw.</summary>
    /// <param name="command">The command's id.</param>
    /// <param name="id">The project the owner named.</param>
    /// <param name="project">The project as the view holds it, or null when there is none.</param>
    /// <param name="seenVersion">The version the owner saw.</param>
    /// <returns>The event to append, SND-PROJECT-404 or SND-PROJECT-409.</returns>
    public static Result<ProjectDetached> Detach(CommandId command, ProjectId id, ProjectAttachedRow? project, long seenVersion) => throw new NotSupportedException();

    /// <summary>Hides or unhides a led Linear project that is still at the hide version the owner saw (0 when it was never hidden).</summary>
    /// <param name="command">The command's id.</param>
    /// <param name="linear">The led project ('linear:project/&lt;id&gt;').</param>
    /// <param name="hidden">True to hide, false to unhide.</param>
    /// <param name="current">The hide row, or null when it was never hidden.</param>
    /// <param name="seenVersion">The hide version the owner saw.</param>
    /// <returns>The event to append, SND-PROJECT-004 for a bad key, or SND-PROJECT-409.</returns>
    public static Result<ProjectHidden> Hide(CommandId command, string linear, bool hidden, ProjectHiddenRow? current, long seenVersion) => throw new NotSupportedException();

    /// <summary>The refusal for a Linear project the store doesn't hold (SND-PROJECT-003), naming the teams Sindri syncs.</summary>
    /// <param name="teams">The synced Linear team keys.</param>
    /// <returns>The error.</returns>
    public static Error NotSynced(IReadOnlyList<string> teams) => throw new NotSupportedException();
}
