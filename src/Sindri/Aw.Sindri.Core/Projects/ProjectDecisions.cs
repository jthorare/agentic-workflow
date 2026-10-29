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
    private const int NoteMax = 200;
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
        else if (NameError(draft.Name) is null)
        {
            // A Linear project keeps the title Linear had when it was attached, so the card has one while Context is down.
            name = (draft.Name ?? string.Empty).Trim();
        }

        var fields = Fields(draft.State, draft.Links, draft.Note);
        return fields.IsError
            ? fields.Error
            : new ProjectAttached(command, id, draft.Linear, name, draft.State, fields.Value, draft.Note, draft.Target);
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
            : new ProjectChanged(command, id, found.Value.Version + 1, edit.Name?.Trim(), edit.State, edit.ClearState, edit.Links?.Select(static l => l.Trim()).ToList(), edit.Note, edit.ClearNote, edit.Target, edit.ClearTarget);
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
        ?? FieldError(edit.State, edit.Links, edit.Note)
        ?? (IsEmpty(edit) ? Invalid("change", "nothing to change (give a name, state, links, note or target)") : null);

    private static Error? RenameError(ProjectAttachedRow project, string? name) =>
        name is null ? null
        : project.Linear is not null ? Invalid("name", "a Linear project keeps Linear's name")
        : NameError(name);

    private static Error? ClearAndSetError(ProjectEdit edit) =>
        edit.State is not null && edit.ClearState ? Invalid("state", "can't be set and cleared in one change")
        : edit.Note is not null && edit.ClearNote ? Invalid("note", "can't be set and cleared in one change")
        : edit.Target is not null && edit.ClearTarget ? Invalid("target", "can't be set and cleared in one change")
        : null;

    private static bool IsEmpty(ProjectEdit edit) =>
        edit is { Name: null, State: null, ClearState: false, Links: null, Note: null, ClearNote: false, Target: null, ClearTarget: false };

    private static Result<string> Name(string? name) => NameError(name) is { } error ? error : (name ?? string.Empty).Trim();

    private static Error? NameError(string? name)
    {
        var trimmed = name?.Trim();
        return string.IsNullOrEmpty(trimmed) ? Invalid("name", "a hand-made project needs a name")
            : trimmed.Length > NameMax ? Invalid("name", string.Create(CultureInfo.InvariantCulture, $"is {trimmed.Length} characters (at most {NameMax})"))
            : null;
    }

    private static Result<IReadOnlyList<string>> Fields(ProjectState? state, IReadOnlyList<string>? links, string? note) =>
        FieldError(state, links, note) is { } error ? error : (links ?? []).Select(static l => l.Trim()).ToList();

    private static Error? FieldError(ProjectState? state, IReadOnlyList<string>? links, string? note) =>
        StateError(state) ?? NoteError(note) ?? LinksError(links);

    private static Error? StateError(ProjectState? state) =>
        state is { } word && !Enum.IsDefined(word)
            ? Invalid("state", string.Create(CultureInfo.InvariantCulture, $"{(int)word} isn't one of backlog, planned, inProgress, paused, done, canceled"))
            : null;

    private static Error? NoteError(string? note) =>
        note is null ? null
        : note.Length > NoteMax ? Invalid("note", string.Create(CultureInfo.InvariantCulture, $"is {note.Length} characters (at most {NoteMax})"))
        : note.Contains('\n', StringComparison.Ordinal) || note.Contains('\r', StringComparison.Ordinal) ? Invalid("note", "must be one line")
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
