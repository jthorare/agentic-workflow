using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Projects;
using Aw.Sindri.Features.AttachProject;
using Aw.Sindri.Features.HideProject;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri project add|edit|remove|hide|unhide' (projects page spec §5.3): clients of the four project write routes. Human verbs start the control
/// plane unless --no-start; inside an agent session they never do (API spec §7) and the routes refuse the session. Edit, remove, hide and unhide need
/// --version: the read that would supply it (GET /sindri/projects) comes with 'sindri projects'.
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

        var version = command.Version ?? 0;   // Parse refused a missing one
        if (command.Verb is ProjectVerbArguments.Verb.Hide or ProjectVerbArguments.Verb.Unhide)
        {
            return await client.Post<HideProjectRequest, ProjectAccepted>(Root + "/hide", new HideProjectRequest(command.Project, command.Verb == ProjectVerbArguments.Verb.Hide, version), ct).ConfigureAwait(false);
        }

        if (!ProjectId.TryFrom(command.Project, out var id))
        {
            return SindriErrors.InvalidInput.With($"'{command.Project}' isn't a project id (prj_…); `sindri projects` will list them");
        }

        var route = Root + "/" + Uri.EscapeDataString(id.Value);
        return command.Verb == ProjectVerbArguments.Verb.Remove
            ? await client.Post<DetachProjectBody, ProjectAccepted>(route + "/detach", new DetachProjectBody(version), ct).ConfigureAwait(false)
            : await client.Post<ChangeProjectBody, ProjectAccepted>(route + "/change", new ChangeProjectBody(version, command.Name, command.State, command.ClearState, command.Links.Count == 0 ? null : command.Links, command.Note, command.ClearNote, command.Target, command.ClearTarget), ct).ConfigureAwait(false);
    }
}
