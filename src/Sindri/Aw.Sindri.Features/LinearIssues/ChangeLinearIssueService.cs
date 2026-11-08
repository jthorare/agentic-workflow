using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Linear;

namespace Aw.Sindri.Features.LinearIssues;

/// <summary>
/// Edits a stored Linear issue (Linear hub spec 2026-11-04 §3, §5.3, D6), in this order: the write gate (a session, the write key, a picked team); the set fields read and every id checked against the team's stored lookups
/// (SND-LINEAR-006, nothing is sent); Linear's live read; a change Linear already holds is applied with nothing sent; a field Linear changed since the sheet opened is a conflict with nothing sent; then Context's Change.
/// What Context answers is the ending: applied (the stored issue is re-read for the answer), applied but stored late (the same, with SND-LINEAR-013), unknown (SND-LINEAR-011, the one re-read already ran) or its error.
/// </summary>
/// <param name="gate">Whether the caller may write to the issue's team.</param>
/// <param name="scope">The owner's scope.</param>
/// <param name="lookups">The team's stored states, members, labels and cycles.</param>
/// <param name="issues">The stored issues: a parent key resolves through them.</param>
/// <param name="writes">Context's Linear writes.</param>
/// <param name="reread">The sheet's read: the issue the answer carries.</param>
internal sealed class ChangeLinearIssueService(
    ILinearWriteGate gate,
    ILinearWriteScope scope,
    ILinearSideFacts lookups,
    ILinearIssueFacts issues,
    IContextLinearWrites writes,
    IService<GetLinearIssueRequest, GetLinearIssueResponse> reread) : IService<ChangeLinearIssueRequest, LinearChangeResult>
{
    private const string IssuePrefix = "linear:issue/";

    public async ValueTask<Result<LinearChangeResult>> Handle(ChangeLinearIssueRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var parsed = LinearIssueKey.Parse(request.Key);
        if (parsed.IsError)
        {
            return parsed.Error;
        }

        var key = parsed.Value;
        var prepared = await Prepare(request, key, cancellationToken).ConfigureAwait(false);
        if (prepared.IsError)
        {
            return prepared.Error;
        }

        var (set, opened, parent, live, owner) = prepared.Value;
        if (set.All(f => Holds(live, f, parent)))
        {
            return await Applied(key, warning: null, alreadyApplied: true, cancellationToken).ConfigureAwait(false);
        }

        var differing = Conflicts(live, opened);
        return differing.Count > 0
            ? await Conflict(key, live, differing, cancellationToken).ConfigureAwait(false)
            : await Send(key, LinearChangeFields.ToChange(set, parent), owner, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<LinearChangeResult>> Send(LinearIssueKey key, LinearIssueChange change, ContextScope owner, CancellationToken cancellationToken)
    {
        var outcome = await writes.Change(key, change, owner, cancellationToken).ConfigureAwait(false);
        if (outcome.IsError)
        {
            return RateLimit(outcome.Error);
        }

        return outcome.Value switch
        {
            LinearWriteApplied => await Applied(key, warning: null, alreadyApplied: null, cancellationToken).ConfigureAwait(false),
            LinearWriteAppliedNotStored => await Applied(key, LinearHubErrors.NotStored.Code, alreadyApplied: null, cancellationToken).ConfigureAwait(false),
            _ => NoAnswer(key),
        };
    }

    // why: the gate comes before anything about the request is read, so a refused caller learns nothing from the field errors
    private async ValueTask<Result<Edit>> Prepare(ChangeLinearIssueRequest request, LinearIssueKey key, CancellationToken cancellationToken)
    {
        if (await gate.Refusal(key.Team, cancellationToken).ConfigureAwait(false) is { } refusal)
        {
            return refusal;
        }

        var fields = Fields(request);
        if (fields.IsError)
        {
            return fields.Error;
        }

        var parent = await Checked(key, fields.Value.Set, cancellationToken).ConfigureAwait(false);
        if (parent.IsError)
        {
            return parent.Error;
        }

        if (Unopened(fields.Value.Set, fields.Value.Opened) is { } unopened)
        {
            return unopened;
        }

        var owner = await scope.Owner(cancellationToken).ConfigureAwait(false);
        if (owner.IsError)
        {
            return owner.Error;
        }

        var live = await writes.Live(key, owner.Value, cancellationToken).ConfigureAwait(false);
        return live.IsError ? live.Error : new Edit(fields.Value.Set, fields.Value.Opened, parent.Value, live.Value, owner.Value);
    }

    private sealed record Edit(IReadOnlyList<KeyValuePair<string, object?>> Set, IReadOnlyList<KeyValuePair<string, object?>> Opened, ExternalId? Parent, LinearIssueView Live, ContextScope Owner);

    private static Result<(IReadOnlyList<KeyValuePair<string, object?>> Set, IReadOnlyList<KeyValuePair<string, object?>> Opened)> Fields(ChangeLinearIssueRequest request)
    {
        var set = LinearChangeFields.Read(request.Set, "set");
        var opened = LinearChangeFields.Read(request.Opened, "opened");
        return set.IsError ? set.Error
            : opened.IsError ? opened.Error
            : set.Value.Count == 0 ? SindriErrors.InvalidInput.With("set names no field to change (fix: send the fields the edit changed)")
            : (set.Value, opened.Value);
    }

    // why: a field sent without the value the sheet opened has nothing to compare with Linear's live value, so it would overwrite a change made meanwhile without a conflict (spec §5.3, D6)
    private static Error? Unopened(IReadOnlyList<KeyValuePair<string, object?>> set, IReadOnlyList<KeyValuePair<string, object?>> opened)
    {
        var missing = set.Select(static f => f.Key).Where(name => !opened.Any(o => o.Key == name)).ToList();
        return missing.Count == 0
            ? null
            : SindriErrors.LinearBadValue.With($"{string.Join(" and ", missing)} {(missing.Count == 1 ? "has" : "have")} no opened value (fix: send the value you opened with each field you change)");
    }

    // why: the ids the owner sends come from the lookups the app read; one that is not in them is a stale sheet or a typo, and Linear's own refusal would say less about which
    private async ValueTask<Result<ExternalId?>> Checked(LinearIssueKey key, IReadOnlyList<KeyValuePair<string, object?>> set, CancellationToken cancellationToken)
    {
        var team = await lookups.Lookups(key.Team, cancellationToken).ConfigureAwait(false);
        if (team.IsError || team.Value is not { } found)
        {
            return team.IsError ? team.Error : LinearWriteRefusals.TeamNotPicked(key.Team);
        }

        foreach (var (name, value) in set)
        {
            if (NotInLookups(found, name, value) is { } wrong)
            {
                return SindriErrors.LinearBadValue.With($"{name} isn't one of {key.Team}'s {wrong}s (fix: read the lookups again and pick a {wrong} from them)");
            }
        }

        return set.FirstOrDefault(static f => f.Key == LinearChangeFields.ParentKey).Value is string parentKey
            ? await Parent(parentKey, cancellationToken).ConfigureAwait(false)
            : Results.Success<ExternalId?>(value: null);
    }

    private static string? NotInLookups(LinearLookupsResponse team, string name, object? value) => name switch
    {
        LinearChangeFields.StateId when value is string id && !team.States.Any(s => s.Uuid == id) => "state",
        LinearChangeFields.AssigneeId when value is string id && !team.Members.Any(m => m.Uuid == id) => "member",
        LinearChangeFields.LabelIds when value is string[] ids && ids.Any(id => !team.Labels.Any(l => l.Uuid == id)) => "label",
        LinearChangeFields.CycleId when value is string id && !team.Cycles.Any(c => c.Uuid == id) => "cycle",
        _ => null,
    };

    private async ValueTask<Result<ExternalId?>> Parent(string parentKey, CancellationToken cancellationToken)
    {
        var found = await issues.Detail(parentKey, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        var record = found.Value?.Row.Record;
        return record is not null && record.StartsWith(IssuePrefix, StringComparison.Ordinal) && ExternalId.TryFrom(record[IssuePrefix.Length..], out var id)
            ? Results.Success<ExternalId?>(id)
            : SindriErrors.LinearBadValue.With("parentKey isn't an issue in your teams (fix: read the lookups again and pick an issue from them)");
    }

    private static bool Holds(LinearIssueView live, KeyValuePair<string, object?> field, ExternalId? parent) =>
        field.Key == LinearChangeFields.ParentKey
            ? string.Equals(live.ParentId?.Value, parent?.Value, StringComparison.Ordinal)
            : LinearChangeFields.Same(LinearChangeFields.Live(live, field.Key), field.Value);

    // why: only the fields the sheet changed are compared, as D6 says; parentKey is left out because Linear's live read names a parent by id, not by key
    private static List<LinearFieldConflict> Conflicts(LinearIssueView live, IReadOnlyList<KeyValuePair<string, object?>> opened) => [.. opened
        .Where(static f => f.Key != LinearChangeFields.ParentKey)
        .Select(f => (f.Key, Opened: f.Value, Linear: LinearChangeFields.Live(live, f.Key)))
        .Where(static f => !LinearChangeFields.Same(f.Opened, f.Linear))
        .Select(static f => new LinearFieldConflict(f.Key, LinearChangeFields.Json(f.Opened), LinearChangeFields.Json(f.Linear)))];

    private async ValueTask<Result<LinearChangeResult>> Applied(LinearIssueKey key, string? warning, bool? alreadyApplied, CancellationToken cancellationToken)
    {
        var issue = await reread.Handle(new GetLinearIssueRequest(key.ToString()), cancellationToken).ConfigureAwait(false);
        return issue.IsError ? issue.Error : new LinearChangeResult(new LinearChangeApplied("applied", warning, alreadyApplied, issue.Value), Conflict: null);
    }

    // why: the stored copy may be older than Linear's, so the conflict carries the stored issue with the fields Linear has now laid over it (names from the team's lookups)
    private async ValueTask<Result<LinearChangeResult>> Conflict(LinearIssueKey key, LinearIssueView live, List<LinearFieldConflict> fields, CancellationToken cancellationToken)
    {
        var stored = await reread.Handle(new GetLinearIssueRequest(key.ToString()), cancellationToken).ConfigureAwait(false);
        var team = await lookups.Lookups(key.Team, cancellationToken).ConfigureAwait(false);
        if (stored.IsError || team.IsError)
        {
            return stored.IsError ? stored.Error : team.Error;
        }

        return new LinearChangeResult(Applied: null, new LinearChangeConflict(live.UpdatedAt, fields, Overlay(stored.Value, live, team.Value)));
    }

    private static GetLinearIssueResponse Overlay(GetLinearIssueResponse stored, LinearIssueView live, LinearLookupsResponse? team) => stored with
    {
        Title = live.Title,
        Description = live.Description ?? string.Empty,
        State = StateOf(stored, live, team),
        Priority = live.Priority,
        Assignee = AssigneeOf(live, team),
        Project = ProjectOf(stored, live),
        Cycle = live.CycleId is { } cycle && team?.Cycles.FirstOrDefault(c => c.Uuid == cycle.Value) is { } found ? new LinearCycleView(found.Uuid, found.Number, found.Name) : null,
        Labels = [.. live.LabelIds.Select(id => LabelOf(stored, id.Value, team))],
        UpdatedAt = live.UpdatedAt,
    };

    private static LinearPersonView? AssigneeOf(LinearIssueView live, LinearLookupsResponse? team) =>
        live.AssigneeId is { } who ? new LinearPersonView(who.Value, team?.Members.FirstOrDefault(m => m.Uuid == who.Value)?.Name ?? who.Value) : null;

    private static LinearNamedView? ProjectOf(GetLinearIssueResponse stored, LinearIssueView live) =>
        live.ProjectId is not { } project ? null : stored.Project is { } known && known.Id == project.Value ? known : new LinearNamedView(project.Value, project.Value);

    private static LinearStateView StateOf(GetLinearIssueResponse stored, LinearIssueView live, LinearLookupsResponse? team) =>
        team?.States.FirstOrDefault(s => s.Uuid == live.StateId.Value) is { } state ? new LinearStateView(state.Uuid, state.Name, state.Type) : new LinearStateView(live.StateId.Value, live.StateName, stored.State.Type);

    private static LinearLabelView LabelOf(GetLinearIssueResponse stored, string id, LinearLookupsResponse? team) =>
        stored.Labels.FirstOrDefault(l => l.Id == id)
        ?? team?.Labels.Where(l => l.Uuid == id).Select(static l => new LinearLabelView(l.Uuid, l.Name, l.Color)).FirstOrDefault()
        ?? new LinearLabelView(id, id, Color: null);

    private static Error NoAnswer(LinearIssueKey key) =>
        LinearHubErrors.NoAnswer.With($"Linear didn't confirm the change to {key}, and the re-read couldn't tell (fix: retry; the same edit is safe to send twice)").WithExtension("outcome", LinearChangeFields.Text("unknown"));

    // why: the sheet shows when to retry; Context names it in its message and, when it can, in a retryAfter member
    private static Error RateLimit(Error error) =>
        error.Code == LinearHubErrors.RateLimited.Code && error.Extensions?.FirstOrDefault(static m => m.Key == "retryAfter").Value is { ValueKind: System.Text.Json.JsonValueKind.String } at
        && DateTimeOffset.TryParse(at.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var when)
            ? error with { Message = $"Linear is rate-limiting this key; nothing was sent (fix: retry after {when.UtcDateTime.ToString("HH:mm", CultureInfo.InvariantCulture)})" }
            : error;
}
