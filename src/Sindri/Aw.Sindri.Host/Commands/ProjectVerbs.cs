using System.Globalization;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Projects;
using Aw.Sindri.Core.Projects.Cards;
using Aw.Sindri.Core.Projects.Vocabulary;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.AttachProject;
using Aw.Sindri.Features.GetProject;
using Aw.Sindri.Features.HideProject;
using Aw.Sindri.Features.ListProjects;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri projects', 'sindri project show' and 'sindri project add|edit|remove|hide|unhide' (projects page spec §5.3): clients of the project routes.
/// Human verbs start the control plane unless --no-start; inside an agent session they never do (API spec §7) and the write routes refuse the session.
/// Edit, remove, hide and unhide read the project's version first when --version is left out, as 'item close' does. The two reads print the answer
/// as the route gave it: for a session the route has already fenced Linear's text.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
/// <param name="ensureRunning">Starts the control plane (true: this verb may launch it) or names why not; null: never start it.</param>
internal sealed class ProjectVerbs(ControlPlaneClient client, Output output, Func<bool, CancellationToken, Task<Error?>>? ensureRunning = null)
{
    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        var parsed = ProjectVerbArguments.Parse(args);
        if (parsed.IsError)
        {
            return await output.Error(parsed.Error).ConfigureAwait(false);
        }

        var command = parsed.Value;
        var down = ensureRunning is null ? null : await ensureRunning(!command.NoStart, ct).ConfigureAwait(false);
        if (down is not null)
        {
            return await output.Error(down).ConfigureAwait(false);
        }

        if (command.Verb is ProjectVerbArguments.Verb.List or ProjectVerbArguments.Verb.Show)
        {
            return await Read(command, ct).ConfigureAwait(false);
        }

        var sent = await Send(command, ct).ConfigureAwait(false);
        if (sent.IsError)
        {
            return await output.Error(sent.Error).ConfigureAwait(false);
        }

        var answer = sent.Value;
        return command.Json
            ? await output.Raw(answer.Body, 0).ConfigureAwait(false)
            : await output.Text(Line(command.Verb, answer.Value), 0).ConfigureAwait(false);
    }

    private async Task<int> Read(ProjectVerbArguments.Parsed command, CancellationToken ct)
    {
        if (command.Verb == ProjectVerbArguments.Verb.Show)
        {
            var shown = await client.GetView<GetProjectResponse>(Route(command.Project), ct).ConfigureAwait(false);
            if (shown.IsError)
            {
                return await output.Error(shown.Error).ConfigureAwait(false);
            }

            var one = shown.Value.Value;
            var shownCode = Stale(one.AsOf) ? 1 : 0;
            return command.Json
                ? await output.Raw(shown.Value.Body, shownCode).ConfigureAwait(false)
                : await output.Text(RenderShow(one), shownCode).ConfigureAwait(false);
        }

        var answer = await client.GetView<ListProjectsResponse>("/sindri/projects" + UrlQuery.Of([("all", command.All ? "true" : null)]), ct).ConfigureAwait(false);
        if (answer.IsError)
        {
            return await output.Error(answer.Error).ConfigureAwait(false);
        }

        var page = answer.Value.Value;
        var code = Stale(page.AsOf) ? 1 : 0;
        return command.Json
            ? await output.Raw(answer.Value.Body, code).ConfigureAwait(false)
            : await output.Text(RenderList(page), code).ConfigureAwait(false);
    }

    private static bool Stale(AsOf asOf) => asOf.Rebuilding || asOf.Stalled;

    private static string Route(string project) => "/sindri/projects/" + Uri.EscapeDataString(project);

    private static string RenderList(ListProjectsResponse page)
    {
        var text = new StringBuilder();
        if (page.Empty is { } sentence)
        {
            _ = text.AppendLine(sentence);
        }

        foreach (var card in page.Projects)
        {
            _ = text.AppendLine(CardLine(card));
        }

        Fold(text, "Finished", page.Finished);
        Fold(text, "Hidden", page.Hidden);
        if (page.Freshness.Count > 0)
        {
            _ = text.AppendJoin(", ", page.Freshness.Select(static row => $"{row.Source} {row.Text}")).AppendLine();
        }

        return text.ToString();
    }

    private static void Fold(StringBuilder text, string name, IReadOnlyList<ProjectCard> cards)
    {
        if (cards.Count == 0)
        {
            return;
        }

        _ = text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{name} ({cards.Count})"));
        foreach (var card in cards)
        {
            _ = text.Append("  ").AppendLine(CardLine(card));
        }
    }

    private static string CardLine(ProjectCard card) =>
        $"{card.Key}  {StateWord(card.State)}  {card.Name}  {Issues(card)}  {Instant(card.LastActivity)}";

    private static string Issues(ProjectCard card) =>
        card.Counting ? "counting…" : card.OpenIssues is { } n ? string.Create(CultureInfo.InvariantCulture, $"{n} issues") : "-";

    private static string Instant(DateTimeOffset? at) => at is { } when ? when.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) : "-";

    private static string StateWord(ProjectState? state) => state switch
    {
        null => "-",
        ProjectState.Backlog => "backlog",
        ProjectState.Planned => "planned",
        ProjectState.InProgress => "in-progress",
        ProjectState.Paused => "paused",
        ProjectState.Done => "done",
        ProjectState.Canceled => "canceled",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "not a project state"),
    };

    private static string RenderShow(GetProjectResponse shown)
    {
        var card = shown.Project;
        var text = new StringBuilder();
        _ = text.AppendLine(CardLine(card));
        Field(text, "origin", card.Origin.ToString().ToLowerInvariant());
        Field(text, "hidden", shown.Hidden ? "yes" : null);
        Field(text, "description", card.Description);
        Field(text, "state set by you", card.StateSetByOwner ? "yes" : null);
        Field(text, "health", card.Health?.ToString());
        Field(text, "lead", card.Lead?.Name);
        Field(text, "target", card.Target?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Field(text, "finished", card.FinishedAt is { } done ? Instant(done) : null);
        Field(text, "note", card.Note);
        Field(text, "synced", card.Synced ? null : "no longer synced from Linear");
        Field(text, "version", card.Version.ToString(CultureInfo.InvariantCulture));
        foreach (var link in card.Links)
        {
            _ = text.Append("  ").Append(link.Label).Append(": ").AppendLine(link.Url);
        }

        if (shown.Freshness.Count > 0)
        {
            _ = text.AppendJoin(", ", shown.Freshness.Select(static row => $"{row.Source} {row.Text}")).AppendLine();
        }

        return text.ToString();
    }

    private static void Field(StringBuilder text, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _ = text.Append("  ").Append(name).Append(": ").AppendLine(value);
        }
    }

    private static string Line(ProjectVerbArguments.Verb verb, ProjectAccepted accepted)
    {
        var sequence = accepted.Accepted.Sequence;
        return verb switch
        {
            ProjectVerbArguments.Verb.Add => string.Create(CultureInfo.InvariantCulture, $"{accepted.Project} attached: {accepted.Name} (sequence {sequence})\n"),
            ProjectVerbArguments.Verb.Edit => string.Create(CultureInfo.InvariantCulture, $"{accepted.Project} changed (sequence {sequence})\n"),
            ProjectVerbArguments.Verb.Remove => string.Create(CultureInfo.InvariantCulture, $"{accepted.Project} removed (sequence {sequence})\n"),
            ProjectVerbArguments.Verb.Hide => string.Create(CultureInfo.InvariantCulture, $"{accepted.Name} hidden (sequence {sequence})\n"),
            ProjectVerbArguments.Verb.Unhide => string.Create(CultureInfo.InvariantCulture, $"{accepted.Name} unhidden (sequence {sequence})\n"),
            ProjectVerbArguments.Verb.List or ProjectVerbArguments.Verb.Show => throw new ArgumentOutOfRangeException(nameof(verb), verb, "a read, not a write"),
            ProjectVerbArguments.Verb.None => throw new ArgumentOutOfRangeException(nameof(verb), verb, "not a project verb"),
            _ => throw new ArgumentOutOfRangeException(nameof(verb), verb, "not a project verb"),
        };
    }

    private async Task<Result<ApiAnswer<ProjectAccepted>>> Send(ProjectVerbArguments.Parsed command, CancellationToken ct)
    {
        const string Root = "/sindri/projects";
        if (command.Verb == ProjectVerbArguments.Verb.Add)
        {
            return await client.Post<AttachProjectRequest, ProjectAccepted>(Root, new AttachProjectRequest(command.Linear, command.Name, command.State, command.Links.Count == 0 ? null : command.Links, command.Note, command.Target), ct).ConfigureAwait(false);
        }

        var version = await Version(command, ct).ConfigureAwait(false);
        if (version.IsError)
        {
            return version.Error;
        }

        if (command.Verb is ProjectVerbArguments.Verb.Hide or ProjectVerbArguments.Verb.Unhide)
        {
            return await client.Post<HideProjectRequest, ProjectAccepted>(Root + "/hide", new HideProjectRequest(command.Project, command.Verb == ProjectVerbArguments.Verb.Hide, version.Value), ct).ConfigureAwait(false);
        }

        if (!ProjectId.TryFrom(command.Project, out var id))
        {
            return SindriErrors.InvalidInput.With($"'{command.Project}' isn't a project id (prj_…); `sindri projects` will list them");
        }

        var route = Root + "/" + Uri.EscapeDataString(id.Value);
        return command.Verb == ProjectVerbArguments.Verb.Remove
            ? await client.Post<DetachProjectBody, ProjectAccepted>(route + "/detach", new DetachProjectBody(version.Value), ct).ConfigureAwait(false)
            : await client.Post<ChangeProjectBody, ProjectAccepted>(route + "/change", new ChangeProjectBody(version.Value, command.Name, command.State, command.ClearState, command.Links.Count == 0 ? null : command.Links, command.Note, command.ClearNote, command.Target, command.ClearTarget), ct).ConfigureAwait(false);
    }

    /// <summary>The version a write sends: the one typed, or the card's own, read now (as 'item close' does).</summary>
    private async Task<Result<long>> Version(ProjectVerbArguments.Parsed command, CancellationToken ct)
    {
        if (command.Version is { } typed)
        {
            return typed;
        }

        var read = await client.GetView<GetProjectResponse>(Route(command.Project), ct).ConfigureAwait(false);
        return read.IsError ? read.Error : read.Value.Value.Project.Version;
    }
}
