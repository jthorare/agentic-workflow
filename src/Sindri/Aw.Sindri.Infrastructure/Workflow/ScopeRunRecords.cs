using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>
/// The agent runs of a scoping instance from the ledger (issue 966, spec 6): each 'launch.started' of the instance joined to the latest 'session.usage' of its launch, and each completion
/// of an agent Step's state that has no launch as a run at the desk (its model is not known). The state's Step and the model it asks for come from the recipe the instance runs.
/// Oldest first, by ledger position.
/// </summary>
/// <param name="ledger">The ledger, read-only.</param>
/// <param name="events">Reads a payload.</param>
/// <param name="instances">The instance, for the recipe it runs.</param>
/// <param name="recipes">The approved recipes, for each state's Step and requested model.</param>
/// <param name="catalog">The blocks' descriptors, for which Steps an agent runs.</param>
internal sealed class ScopeRunRecords(ILedgerReader ledger, EventFactory events, IInstanceReader instances, IApprovedRecipes recipes, BlockCatalog catalog) : IScopeRunRecords
{
    private const int Page = 500;
    private const string Agent = "agent";
    private const string Desk = "desk";

    public async ValueTask<Result<IReadOnlyList<ScopeRunRecord>>> Of(InstanceAddress run, CancellationToken cancellationToken)
    {
        var states = await States(run, cancellationToken).ConfigureAwait(false);
        if (states.IsError)
        {
            return states.Error;
        }

        var launches = await Launches(run, states.Value, cancellationToken).ConfigureAwait(false);
        if (launches.IsError)
        {
            return launches.Error;
        }

        var spent = await Usage(launches.Value, cancellationToken).ConfigureAwait(false);
        if (spent.IsError)
        {
            return spent.Error;
        }

        var completed = await Completions(run, cancellationToken).ConfigureAwait(false);
        if (completed.IsError)
        {
            return completed.Error;
        }

        return Results.Success(Assemble(run, states.Value, new LedgerRead(launches.Value, spent.Value, completed.Value.Completed, completed.Value.Transitions)));
    }

    // Launched runs, then the completions that have no launch, in ledger order.
    private IReadOnlyList<ScopeRunRecord> Assemble(
        InstanceAddress run,
        Dictionary<string, StateAsk> states,
        LedgerRead read)
    {
        var (launches, spent, completed, transitions) = read;
        var launched = launches.Select(l => (l.Sequence, Record: AgentRecord(run, transitions, l.Started, states[l.Started.State!], spent.GetValueOrDefault(l.Started.Launch.Value))));   // why: Launches keeps only the launches whose state the recipe has
        var launchIds = launches.Select(static l => l.Started.Launch).ToHashSet();
        var atTheDesk = completed
            .Where(c => states.TryGetValue(c.Payload.From, out var ask) && IsAgent(ask) && c.Payload.Run is { } token && !launchIds.Contains(AgentLaunches.LaunchIdFor(WorkflowInstanceId.Of(run), RunToken.From(token))))
            .Select(c => (c.Sequence, Record: DeskRecord(c.Payload, states[c.Payload.From])));
        return [.. launched.Concat(atTheDesk).OrderBy(static r => r.Sequence).Select(static r => r.Record)];
    }

    private static ScopeRunRecord AgentRecord(InstanceAddress run, int transitions, LaunchStarted started, StateAsk ask, (long Tokens, string? Model) spent) => new(
        ask.Step, started.State!, started.Attempt ?? 1, Agent, spent.Model, ask.Requested, VendorOf(spent.Model), spent.Tokens, started.Launch.Value, RunTokenOf(run, transitions, started));   // why: Launches keeps only launches that name their state

    // The launch id is a hash of the instance and the run token, and the event keeps only the token's number. Every frame entry follows a transition the ledger holds for the instance, so the entry is at most that many plus one.
    private static string? RunTokenOf(InstanceAddress run, int transitions, LaunchStarted started)
    {
        if (started.Attempt is not { } number)
        {
            return null;
        }

        var instance = WorkflowInstanceId.Of(run);
        for (var entry = 1; entry <= transitions + 1; entry++)
        {
            var candidate = RunToken.For(entry, number);
            if (AgentLaunches.LaunchIdFor(instance, candidate) == started.Launch)
            {
                return candidate.Value;
            }
        }

        return null;
    }   // why: Launches keeps only launches that name their state

    private static ScopeRunRecord DeskRecord(WorkflowEventPayload completed, StateAsk ask) => new(
        ask.Step, completed.From, RunToken.From(completed.Run!).Number, Desk, Model: null, ask.Requested, Vendor: null, Tokens: 0, Launch: null, RunToken: completed.Run);   // why: Of keeps only completions with a run token

    // The vendor of a model by its id, null when it isn't a family this knows.
    private static string? VendorOf(string? model) => model switch
    {
        null => null,
        _ when model.StartsWith("claude", StringComparison.OrdinalIgnoreCase) => "anthropic",
        _ when model.StartsWith("gpt", StringComparison.OrdinalIgnoreCase) || model.StartsWith("codex", StringComparison.OrdinalIgnoreCase) || model.StartsWith("o1", StringComparison.OrdinalIgnoreCase)
            || model.StartsWith("o3", StringComparison.OrdinalIgnoreCase) || model.StartsWith("o4", StringComparison.OrdinalIgnoreCase) => "openai",
        _ when model.StartsWith("gemini", StringComparison.OrdinalIgnoreCase) => "google",
        _ => null,
    };

    private bool IsAgent(StateAsk ask) => catalog.Find(ask.Use)?.Runtime == BlockRuntime.Agent;

    // Each state of the instance's recipe that runs a Step: the Step's id and the model its 'with.model' literal asks for.
    private async ValueTask<Result<Dictionary<string, StateAsk>>> States(InstanceAddress run, CancellationToken cancellationToken)
    {
        var view = await instances.Get(run, cancellationToken).ConfigureAwait(false);
        if (view.IsError)
        {
            return view.Error;
        }

        var approved = await recipes.Find(view.Value.Recipe.Id, view.Value.Recipe.Hash, cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        // why: the Where keeps only states that run a Step
        return approved.Value.Recipe.States.Where(static s => s.Value.Step is not null)
            .ToDictionary(static s => s.Key, static s => new StateAsk(s.Value.Step!.Use, RequestedModel(s.Value.Step)), StringComparer.Ordinal);
    }

    private static string? RequestedModel(StepUse step) =>
        step.With.TryGetValue("model", out var value) && value.Literal is { ValueKind: JsonValueKind.String } text && text.GetString() is { Length: > 0 } id ? id : null;

    // Every 'launch.started' of the instance in a state the recipe has, oldest first. The launch events are keyed by launch, not by instance, so they are read by type and kept by their payload.
    private async ValueTask<Result<List<(long Sequence, LaunchStarted Started, string? Item)>>> Launches(InstanceAddress run, Dictionary<string, StateAsk> states, CancellationToken cancellationToken)
    {
        var scanned = await Scan(new LedgerFilter(TypeFilter.Parse(EventTypes.LaunchStarted).Value, Item: null), cancellationToken).ConfigureAwait(false);
        if (scanned.IsError)
        {
            return scanned.Error;
        }

        var kept = new List<(long, LaunchStarted, string?)>();
        foreach (var recorded in scanned.Value)
        {
            var payload = events.Payload<LaunchStarted>(recorded);
            if (payload.IsError)
            {
                return payload.Error;
            }

            if (payload.Value.Instance is { } instance && string.Equals(instance.Value, run.Value, StringComparison.Ordinal) && payload.Value.State is { } state && states.ContainsKey(state))
            {
                kept.Add((recorded.Sequence, payload.Value, recorded.Item));
            }
        }

        return kept;
    }

    // The latest sample of each launch: the tokens a Step's budget counts (as the sessions port counts them) and the model of the latest sample that named one.
    private async ValueTask<Result<Dictionary<string, (long Tokens, string? Model)>>> Usage(List<(long Sequence, LaunchStarted Started, string? Item)> launches, CancellationToken cancellationToken)
    {
        var latest = new Dictionary<string, (int Sample, long Tokens, string? Model)>(StringComparer.Ordinal);
        foreach (var item in launches.Select(static l => l.Item).OfType<string>().Distinct(StringComparer.Ordinal))
        {
            var scanned = await Scan(new LedgerFilter(TypeFilter.Parse(EventTypes.SessionUsage).Value, ItemId.From(item)), cancellationToken).ConfigureAwait(false);
            if (scanned.IsError)
            {
                return scanned.Error;
            }

            foreach (var recorded in scanned.Value)
            {
                var payload = events.Payload<SessionUsage>(recorded);
                if (payload.IsError)
                {
                    return payload.Error;
                }

                var usage = payload.Value;
                var (beforeSample, _, beforeModel) = latest.GetValueOrDefault(usage.Launch.Value);
                if (usage.Sample > beforeSample)
                {
                    latest[usage.Launch.Value] = (usage.Sample, usage.InputTokens + usage.OutputTokens - (usage.CacheReadTokens ?? 0), usage.Model ?? beforeModel);
                }
            }
        }

        return latest.ToDictionary(static l => l.Key, static l => (l.Value.Tokens, l.Value.Model), StringComparer.Ordinal);
    }

    // Every 'workflow.transition' row the engine mirrored for the instance that completed a Step, oldest first.
    private async ValueTask<Result<(List<(long Sequence, WorkflowEventPayload Payload)> Completed, int Transitions)>> Completions(InstanceAddress run, CancellationToken cancellationToken)
    {
        var scanned = await Scan(new LedgerFilter(TypeFilter.Parse(EventTypes.WorkflowTransition).Value, Item: null, Instance: run), cancellationToken).ConfigureAwait(false);
        if (scanned.IsError)
        {
            return scanned.Error;
        }

        var kept = new List<(long, WorkflowEventPayload)>();
        foreach (var recorded in scanned.Value)
        {
            var payload = events.Payload<WorkflowEventPayload>(recorded);
            if (payload.IsError)
            {
                return payload.Error;
            }

            if (string.Equals(payload.Value.Trigger, TriggerNames.StepCompleted, StringComparison.Ordinal))
            {
                kept.Add((recorded.Sequence, payload.Value));
            }
        }

        return (kept, scanned.Value.Count);
    }

    // All the rows a filter keeps, page by page, oldest first.
    private async ValueTask<Result<List<LedgerEvent>>> Scan(LedgerFilter filter, CancellationToken cancellationToken)
    {
        var head = await ledger.Head(cancellationToken).ConfigureAwait(false);
        if (head.IsError)
        {
            return head.Error;
        }

        var rows = new List<LedgerEvent>();
        var cursor = 0L;
        while (cursor < head.Value)
        {
            var scan = await ledger.Scan(cursor, Page, filter, cancellationToken).ConfigureAwait(false);
            if (scan.IsError)
            {
                return scan.Error;
            }

            rows.AddRange(scan.Value.Events);
            if (scan.Value.ScannedTo <= cursor)
            {
                break;
            }

            cursor = scan.Value.ScannedTo;
        }

        return rows;
    }

    // What the ledger scans found: the launches, their latest usage, the Step completions, and how many transitions the instance has.
    private sealed record LedgerRead(
        List<(long Sequence, LaunchStarted Started, string? Item)> Launches,
        Dictionary<string, (long Tokens, string? Model)> Spent,
        List<(long Sequence, WorkflowEventPayload Payload)> Completed,
        int Transitions);

    private sealed record StateAsk(BlockId Use, string? Requested)
    {
        public string Step => Use.Value;
    }
}
