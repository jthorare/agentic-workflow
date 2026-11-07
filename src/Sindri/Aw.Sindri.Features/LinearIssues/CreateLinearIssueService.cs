using Aw.Context.Core.Facade;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Linear;

namespace Aw.Sindri.Features.LinearIssues;

/// <summary>
/// Creates an issue as the owner (Linear hub spec §5.2; desk). In order: a session is refused (SND-AUTH-002); the client id is read (SND-CLI-003); the team must be a picked one (SND-LINEAR-003); every id in the body must be one of the team's stored
/// lookups (SND-LINEAR-006), the parent key a stored issue; then Context creates it with the client id, so a retry never creates twice. The answer is the new issue as the list shows it, read back through <see cref="GetLinearIssueService"/>.
/// A write that Linear never confirmed is SND-LINEAR-011 with the client id to retry with.
/// </summary>
/// <param name="scopes">The owner's scope.</param>
/// <param name="engine">Context's Linear writes.</param>
/// <param name="teams">The picked-team check and the team's lookups.</param>
/// <param name="facts">The stored issues, for the parent.</param>
/// <param name="issues">The issue read the answer is taken from.</param>
internal sealed class CreateLinearIssueService(LinearScopes scopes, IContextLinearWrites engine, LinearTeamGate teams, ILinearIssueFacts facts, IService<GetLinearIssueRequest, GetLinearIssueResponse> issues)
    : IService<CreateLinearIssueRequest, CreateLinearIssueResponse>
{
    private const string IssueRecord = "linear:issue/";
    private const string NotStored = "SND-LINEAR-013";

    public async ValueTask<Result<CreateLinearIssueResponse>> Handle(CreateLinearIssueRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (scopes.IsSession())
        {
            return SindriErrors.AuthRefused.With(LinearWriteFailures.Refusal);
        }

        var client = LinearClientId.Parse(request.ClientId);
        if (client.IsError)
        {
            return SindriErrors.InvalidInput.With(client.Error.Message);
        }

        if (string.IsNullOrWhiteSpace(request.Team))
        {
            return SindriErrors.InvalidInput.With("team is the team's key, 'ENG'");
        }

        var team = await teams.Picked(request.Team.Trim().ToUpperInvariant(), cancellationToken).ConfigureAwait(false);
        if (team.IsError)
        {
            return team.Error;
        }

        var draft = await Draft(request, team.Value, cancellationToken).ConfigureAwait(false);
        var scope = await scopes.Read(owner: true, cancellationToken).ConfigureAwait(false);
        if (draft.IsError || scope.IsError)
        {
            return draft.IsError ? draft.Error : scope.Error;
        }

        var sent = await engine.Create(draft.Value, client.Value, scope.Value, cancellationToken).ConfigureAwait(false);
        return sent.IsError
            ? sent.Error.Code == LinearHubErrors.NoAnswer.Code ? Unconfirmed(team.Value.Team.Key, client.Value) : LinearWriteFailures.Of(sent.Error)
            : await Outcome(sent.Value, team.Value, client.Value, cancellationToken).ConfigureAwait(false);
    }

    private static Error Unconfirmed(string team, LinearClientId client) =>
        LinearWriteFailures.NoAnswer($"Linear didn't confirm the new {team} issue (fix: retry; the same client id never creates it twice)", client.Value);

    private async ValueTask<Result<CreateLinearIssueResponse>> Outcome(LinearWriteOutcome outcome, LinearLookupsResponse lookups, LinearClientId client, CancellationToken cancellationToken) => outcome switch
    {
        LinearWriteApplied applied => await Created(applied.Stored, lookups, client, warning: null, cancellationToken).ConfigureAwait(false),
        LinearWriteAppliedNotStored { Live: { } live } => await Created(live, lookups, client, NotStored, cancellationToken).ConfigureAwait(false),
        LinearWriteAppliedNotStored => LinearHubErrors.NotStored.With("Saved in Linear; Sindri shows it after the next sync"),
        _ => Unconfirmed(lookups.Team.Key, client),
    };

    private async ValueTask<Result<CreateLinearIssueResponse>> Created(LinearIssueView view, LinearLookupsResponse lookups, LinearClientId client, string? warning, CancellationToken cancellationToken)
    {
        var read = await issues.Handle(new GetLinearIssueRequest(view.Key.ToString()), cancellationToken).ConfigureAwait(false);
        var row = read.IsSuccess ? Row(read.Value) : Row(view, lookups);
        return new CreateLinearIssueResponse("applied", client.Value, row, warning);
    }

    private static LinearIssueRowView Row(GetLinearIssueResponse r) =>
        new(r.Key, r.Record, r.Url, r.Title, r.Team, r.State, r.Priority, r.Assignee, r.Project, r.Cycle, r.Labels, r.UpdatedAt, r.Link, r.Pr);

    // why: the store missed the issue, so the row is built from what Linear answered and the team's lookups (a project has no lookup here, so it shows by its id)
    private static LinearIssueRowView Row(LinearIssueView view, LinearLookupsResponse lookups) => new(
        view.Key.ToString(), IssueRecord + view.Id.Value, Url: null, view.Title, new LinearTeamView(lookups.Team.Uuid ?? string.Empty, view.TeamKey), State(view, lookups), view.Priority,
        Assignee(view, lookups), view.ProjectId is { } project ? new LinearNamedView(project.Value, project.Value) : null, Cycle(view, lookups), Labels(view, lookups), view.UpdatedAt, Link: null, Pr: null);

    private static LinearStateView State(LinearIssueView view, LinearLookupsResponse lookups) =>
        lookups.States.FirstOrDefault(s => s.Uuid == view.StateId.Value) is { } state ? new LinearStateView(state.Uuid, state.Name, state.Type) : new LinearStateView(view.StateId.Value, view.StateName, "unstarted");

    private static LinearPersonView? Assignee(LinearIssueView view, LinearLookupsResponse lookups) =>
        view.AssigneeId is { } id ? new LinearPersonView(id.Value, lookups.Members.FirstOrDefault(m => m.Uuid == id.Value)?.Name ?? id.Value) : null;

    private static LinearCycleView? Cycle(LinearIssueView view, LinearLookupsResponse lookups) =>
        view.CycleId is { } id && lookups.Cycles.FirstOrDefault(c => c.Uuid == id.Value) is { } cycle ? new LinearCycleView(cycle.Uuid, cycle.Number, cycle.Name) : null;

    private static List<LinearLabelView> Labels(LinearIssueView view, LinearLookupsResponse lookups) =>
        [.. view.LabelIds.Select(l => lookups.Labels.FirstOrDefault(x => x.Uuid == l.Value) is { } label ? new LinearLabelView(label.Uuid, label.Name, label.Color) : new LinearLabelView(l.Value, l.Value, Color: null))];

    private async ValueTask<Result<LinearIssueDraft>> Draft(CreateLinearIssueRequest request, LinearLookupsResponse lookups, CancellationToken cancellationToken)
    {
        var refused = CheckIds(request, lookups) ?? CheckLabels(request, lookups);
        if (refused is not null)
        {
            return refused;
        }

        if (lookups.Team.Uuid is not { } teamId || !ExternalId.TryFrom(teamId, out var team))
        {
            return SindriErrors.LinearIdNotInTeam.With($"{lookups.Team.Key} hasn't synced yet, so Sindri has no id for it (fix: wait for the next sync)");
        }

        var parent = await Parent(request.ParentKey, cancellationToken).ConfigureAwait(false);
        if (parent.IsError)
        {
            return parent.Error;
        }

        var project = Id(request.ProjectId);
        if (request.ProjectId is not null && project is null)
        {
            return SindriErrors.InvalidInput.With("projectId isn't a Linear id (fix: pick the project from the list)");
        }

        return new LinearIssueDraft(team, request.Title, request.Description, Id(request.StateId), Id(request.AssigneeId), request.Priority, Estimate: null, Id(request.CycleId), project, parent.Value, [.. (request.LabelIds ?? []).Select(static l => ExternalId.From(l))]);
    }

    private static Error? CheckIds(CreateLinearIssueRequest r, LinearLookupsResponse l) =>
        r.StateId is { } state && !l.States.Any(s => s.Uuid == state) ? LinearTeamGate.NotInTeam("stateId", l.Team.Key, "a state")
        : r.AssigneeId is { } who && !l.Members.Any(m => m.Active && m.Uuid == who) ? LinearTeamGate.NotInTeam("assigneeId", l.Team.Key, "a member")
        : r.CycleId is { } cycle && !l.Cycles.Any(c => c.Uuid == cycle) ? LinearTeamGate.NotInTeam("cycleId", l.Team.Key, "a cycle")
        : null;

    private static Error? CheckLabels(CreateLinearIssueRequest r, LinearLookupsResponse l) =>
        r.Priority is < 0 or > 4 ? SindriErrors.InvalidInput.With("priority is 0 (none) to 4 (low)")
        : r.LabelIds?.FirstOrDefault(id => !l.Labels.Any(x => x.Uuid == id)) is not null ? SindriErrors.LinearIdNotInTeam.With($"labelIds has an id that isn't a label of {l.Team.Key} (fix: read the lookups again and pick labels from them)")
        : null;

    private async ValueTask<Result<ExternalId?>> Parent(string? parentKey, CancellationToken cancellationToken)
    {
        if (parentKey is null)
        {
            return Results.Success<ExternalId?>(value: null);
        }

        var key = LinearIssueKey.Parse(parentKey);
        if (key.IsError)
        {
            return SindriErrors.InvalidInput.With(key.Error.Message);
        }

        var found = await facts.Detail(key.Value.ToString(), cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        return found.Value?.Row.Record is { } record && record.StartsWith(IssueRecord, StringComparison.Ordinal) && ExternalId.TryFrom(record[IssueRecord.Length..], out var id)
            ? Results.Success<ExternalId?>(id)
            : SindriErrors.LinearIdNotInTeam.With($"parentKey isn't an issue Sindri has stored: {key.Value} (fix: pick the parent from the Linear list)");
    }

    private static ExternalId? Id(string? id) => id is not null && ExternalId.TryFrom(id, out var parsed) ? parsed : null;
}
