using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Projects.Vocabulary;

namespace Aw.Sindri.Core.Projects;

/// <summary>The pure decisions of the project commands (projects page spec §5.1, §5.3, §8).</summary>
public static partial class ProjectDecisions
{
    private const int NameMax = 200;
    private const int NotesMax = 16384;
    private const int LinksMax = 10;
    private const string StaleText = "This project changed since you read it. Read it again and retry.";

    /// <summary>Attaches a Linear project or adds a hand-made one.</summary>
    /// <param name="command">The command's id.</param>
    /// <param name="id">The new project's id.</param>
    /// <param name="draft">What the owner typed.</param>
    /// <param name="attached">The projects attached now.</param>
    /// <param name="led">Whether the owner leads the draft's Linear project already (it is on the page without being attached).</param>
    /// <returns>The event to append, SND-PROJECT-004 naming a bad field, or SND-PROJECT-001 for a project already on the page.</returns>
    public static Result<ProjectAttached> Attach(CommandId command, ProjectId id, ProjectDraft draft, IReadOnlyList<ProjectAttachedRow> attached, bool led)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(attached);
        if (draft.Linear is { } linear)
        {
            if (!IsLinearKey(linear))
            {
                return Invalid("linear", $"'{linear}' isn't a Linear project key (expected linear:project/<id>)");
            }

            if (led || attached.Any(row => string.Equals(row.Linear, linear, StringComparison.Ordinal)))
            {
                return SindriErrors.ProjectAlreadyOnPage.With("That project is already on your page.");
            }
        }

        string? name = null;
        if (draft.Linear is null)
        {
            var named = Name(draft.Name);
            if (named.IsError)
            {
                return named.Error;
            }

            name = named.Value;
        }

        var fields = Fields(draft.State, draft.Links, draft.Notes);
        return fields.IsError
            ? fields.Error
            : new ProjectAttached(command, id, draft.Linear, name, draft.State, fields.Value, draft.Target, draft.Notes);
    }

    /// <summary>Changes an attached or hand-made project that is still at the version the owner saw.</summary>
    /// <param name="command">The command's id.</param>
    /// <param name="id">The project the owner named.</param>
    /// <param name="project">The project as the view holds it, or null when there is none.</param>
    /// <param name="edit">What to change.</param>
    /// <param name="seenVersion">The version the owner saw.</param>
    /// <returns>The event to append, SND-PROJECT-404, SND-PROJECT-409, or SND-PROJECT-004 naming a bad field.</returns>
    public static Result<ProjectChanged> Change(CommandId command, ProjectId id, ProjectAttachedRow? project, ProjectEdit edit, long seenVersion)
    {
        ArgumentNullException.ThrowIfNull(edit);
        var found = Found(id, project, seenVersion);
        if (found.IsError)
        {
            return found.Error;
        }

        var refused = Refuse(found.Value, edit);
        return refused is not null
            ? refused
            : new ProjectChanged(command, id, found.Value.Version + 1, edit.Name?.Trim(), edit.State, edit.ClearState, edit.Links?.Select(static l => l.Trim()).ToList(), edit.Target, edit.ClearTarget, edit.Notes, edit.ClearNotes);
    }

    /// <summary>Removes an attached or hand-made project that is still at the version the owner saw.</summary>
    /// <param name="command">The command's id.</param>
    /// <param name="id">The project the owner named.</param>
    /// <param name="project">The project as the view holds it, or null when there is none.</param>
    /// <param name="seenVersion">The version the owner saw.</param>
    /// <returns>The event to append, SND-PROJECT-404 or SND-PROJECT-409.</returns>
    public static Result<ProjectDetached> Detach(CommandId command, ProjectId id, ProjectAttachedRow? project, long seenVersion)
    {
        var found = Found(id, project, seenVersion);
        return found.IsError ? found.Error : new ProjectDetached(command, id, found.Value.Version + 1);
    }

    /// <summary>Hides or unhides a led Linear project that is still at the hide version the owner saw (0 when it was never hidden).</summary>
    /// <param name="command">The command's id.</param>
    /// <param name="linear">The led project ('linear:project/&lt;id&gt;').</param>
    /// <param name="hidden">True to hide, false to unhide.</param>
    /// <param name="current">The hide row, or null when it was never hidden.</param>
    /// <param name="seenVersion">The hide version the owner saw.</param>
    /// <returns>The event to append, SND-PROJECT-004 for a bad key, or SND-PROJECT-409.</returns>
    public static Result<ProjectHidden> Hide(CommandId command, string linear, bool hidden, ProjectHiddenRow? current, long seenVersion)
    {
        if (!IsLinearKey(linear))
        {
            return Invalid("linear", $"'{linear}' isn't a Linear project key (expected linear:project/<id>)");
        }

        var version = current?.Version ?? 0;
        return version == seenVersion ? new ProjectHidden(command, linear, hidden, version + 1) : Stale();
    }

    /// <summary>The refusal for a Linear project the store doesn't hold (SND-PROJECT-003), naming the teams Sindri syncs.</summary>
    /// <param name="teams">The synced Linear team keys.</param>
    /// <returns>The error.</returns>
    public static Error NotSynced(IReadOnlyList<string> teams)
    {
        ArgumentNullException.ThrowIfNull(teams);
        return SindriErrors.ProjectNotSynced.With($"Sindri doesn't sync that project. It syncs these Linear teams: {string.Join(", ", teams)}. Add the team to `context.sources.linear.teams`.");
    }

    /// <summary>Writes the notes of a led Linear project that is still at the card version the owner saw (0 when it has no row yet).</summary>
    /// <param name="command">The command's id.</param>
    /// <param name="linear">The led project ('linear:project/&lt;id&gt;').</param>
    /// <param name="current">The project's row, or null when it was never hidden or noted.</param>
    /// <param name="edit">What to change: only notes or clearNotes.</param>
    /// <param name="seenVersion">The version the owner saw.</param>
    /// <returns>The event to append, SND-PROJECT-004 naming a bad field or any other field, or SND-PROJECT-409.</returns>
    public static Result<ProjectLedNoted> LedNotes(CommandId command, string linear, ProjectHiddenRow? current, ProjectEdit edit, long seenVersion)
    {
        ArgumentNullException.ThrowIfNull(edit);
        if (!IsLinearKey(linear))
        {
            return Invalid("linear", $"'{linear}' isn't a Linear project key (expected linear:project/<id>)");
        }

        var refused = LedEditError(edit);
        if (refused is not null)
        {
            return refused;
        }

        var version = current?.Version ?? 0;
        return version == seenVersion ? new ProjectLedNoted(command, linear, edit.ClearNotes ? null : edit.Notes, version + 1) : Stale();
    }

    // a project the owner leads keeps Linear's name, state, links and target: only the notes are the owner's
    private static Error? LedEditError(ProjectEdit edit) =>
        edit is { Name: not null } or { State: not null } or { ClearState: true } or { Links: not null } or { Target: not null } or { ClearTarget: true }
            ? SindriErrors.ProjectInvalid.With("a project you lead only takes notes here; edit it in Linear")
            : edit.Notes is not null && edit.ClearNotes ? Invalid("notes", "can't be set and cleared in one change")
            : NotesError(edit.Notes) ?? (edit is { Notes: null, ClearNotes: false } ? Invalid("change", "nothing to change (give notes or clear them)") : null);

    /// <summary>Whether a change names a Linear project the owner leads ('linear:project/&lt;id&gt;') rather than a prj_ id.</summary>
    /// <param name="project">What the request named.</param>
    /// <returns>True for a Linear key.</returns>
    public static bool NamesLinearProject(string project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.StartsWith("linear:project/", StringComparison.Ordinal);
    }

    /// <summary>Whether a request sent both 'notes' and its alias 'note' with different values (SND-PROJECT-004).</summary>
    /// <param name="notes">The 'notes' field, or null.</param>
    /// <param name="note">The 'note' alias, or null. The alias is removed one release after the notes field ships (owner q158).</param>
    /// <returns>The error, or null when at most one was sent or both agree.</returns>
    public static Error? NotesAliasConflict(string? notes, string? note) =>
        notes is not null && note is not null && !string.Equals(notes, note, StringComparison.Ordinal)
            ? Invalid("notes", "notes and its old name note were both sent with different values (send notes)")
            : null;

    private static Error Stale() => SindriErrors.ProjectConflict.With(StaleText);

    private static Result<ProjectAttachedRow> Found(ProjectId id, ProjectAttachedRow? project, long seenVersion) =>
        project is null
            ? SindriErrors.ProjectNotFound.With($"No project {id.Value}. `sindri projects` lists them.")
            : project.Version != seenVersion
                ? Stale()
                : project;

    private static Error? Refuse(ProjectAttachedRow project, ProjectEdit edit) =>
        RenameError(project, edit.Name)
        ?? ClearAndSetError(edit)
        ?? FieldError(edit.State, edit.Links, edit.Notes)
        ?? (IsEmpty(edit) ? Invalid("change", "nothing to change (give a name, state, links, notes or target)") : null);

    private static Error? RenameError(ProjectAttachedRow project, string? name) =>
        name is null ? null
        : project.Linear is not null ? Invalid("name", "a Linear project keeps Linear's name")
        : NameError(name);

    private static Error? ClearAndSetError(ProjectEdit edit) =>
        edit.State is not null && edit.ClearState ? Invalid("state", "can't be set and cleared in one change")
        : edit.Notes is not null && edit.ClearNotes ? Invalid("notes", "can't be set and cleared in one change")
        : edit.Target is not null && edit.ClearTarget ? Invalid("target", "can't be set and cleared in one change")
        : null;

    private static bool IsEmpty(ProjectEdit edit) =>
        edit is { Name: null, State: null, ClearState: false, Links: null, Notes: null, ClearNotes: false, Target: null, ClearTarget: false };

    private static Result<string> Name(string? name) => NameError(name) is { } error ? error : (name ?? string.Empty).Trim();

    private static Error? NameError(string? name)
    {
        var trimmed = name?.Trim();
        return string.IsNullOrEmpty(trimmed) ? Invalid("name", "a hand-made project needs a name")
            : trimmed.Length > NameMax ? Invalid("name", string.Create(CultureInfo.InvariantCulture, $"is {trimmed.Length} characters (at most {NameMax})"))
            : null;
    }

    private static Result<IReadOnlyList<string>> Fields(ProjectState? state, IReadOnlyList<string>? links, string? notes) =>
        FieldError(state, links, notes) is { } error ? error : (links ?? []).Select(static l => l.Trim()).ToList();

    private static Error? FieldError(ProjectState? state, IReadOnlyList<string>? links, string? notes) =>
        StateError(state) ?? NotesError(notes) ?? LinksError(links);

    private static Error? StateError(ProjectState? state) =>
        state is { } word && !Enum.IsDefined(word)
            ? Invalid("state", string.Create(CultureInfo.InvariantCulture, $"{(int)word} isn't one of backlog, planned, inProgress, paused, done, canceled"))
            : null;

    private static Error? NotesError(string? notes) =>
        notes is null ? null
        : notes.Length > NotesMax ? Invalid("notes", string.Create(CultureInfo.InvariantCulture, $"is {notes.Length} characters (at most {NotesMax})"))
        : null;

    private static Error? LinksError(IReadOnlyList<string>? links)
    {
        if (links is null)
        {
            return null;
        }

        if (links.Count > LinksMax)
        {
            return Invalid("links", string.Create(CultureInfo.InvariantCulture, $"{links.Count} links (at most {LinksMax})"));
        }

        return links.Select(LinkError).FirstOrDefault(static error => error is not null);
    }

    private static Error? LinkError(string link)
    {
        var trimmed = link?.Trim() ?? string.Empty;
        return !Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.Host.Length == 0 ? Invalid("links", $"'{trimmed}' isn't an https url")
            : uri.Query.Length > 0 || uri.Fragment.Length > 0 ? Invalid("links", $"'{trimmed}' has a query or fragment (links are plain https urls)")
            : null;
    }

    private static Error Invalid(string field, string why) => SindriErrors.ProjectInvalid.With($"{field}: {why}");

    private static bool IsLinearKey(string key) => LinearKey.IsMatch(key);

    [GeneratedRegex("^linear:project/[A-Za-z0-9][A-Za-z0-9-]*\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LinearKey { get; }
}
