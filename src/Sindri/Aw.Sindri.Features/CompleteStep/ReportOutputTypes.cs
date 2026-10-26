using System.Collections.Concurrent;
using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.CompleteStep;

/// <summary>
/// Checks a launched agent's reported outputs against the types its recipe declares (run23: the design draft wrote 'fact:design' as prose and a later Step
/// parked on it): a 'payload' must be a JSON object or array. The first bad report of a run is refused (SND-WF-008) with the fix, which the agent reads from
/// its 'sindri workflow complete' and answers by reporting again; the second ends the session, so the run fails as an 'exit' the normal way. Nothing is
/// stored or sent for a refused report. The count is per run and kept in memory: a restart gives one more try, never fewer.
/// </summary>
/// <param name="recipes">Loads the instance's recipe, whose declarations say which outputs are payloads.</param>
/// <param name="launcher">Ends the session after its second bad report.</param>
/// <param name="refusals">Keeps the second refusal's text for the watch that sees the session exit, so the failed Step names it.</param>
internal sealed class ReportOutputTypes(IApprovedRecipes recipes, ILauncher launcher, ReportRefusals refusals)
{
    private readonly ConcurrentDictionary<(string Instance, string Run), int> _refused = [];

    /// <summary>Checks the report.</summary>
    /// <param name="view">The instance as read; its open run says which launch to end.</param>
    /// <param name="request">The report.</param>
    /// <param name="cancellationToken">Cancels the read and the stop.</param>
    /// <returns>The refusal, or null when every declared output holds.</returns>
    public async ValueTask<Error?> Check(InstanceView view, CompleteStepRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(request);
        if (request.Outputs.Count == 0)
        {
            return null;
        }

        var found = await recipes.Find(view.Recipe.Id, view.Recipe.Hash, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        var declared = PayloadFacts.DeclaredIn(found.Value.Recipe);
        var wrong = request.Outputs.Where(o => declared.Contains(o.Key)).OrderBy(static o => o.Key.Value, StringComparer.Ordinal)
            .Select(o => PayloadFacts.Violation(o.Key, o.Value.Span)).OfType<string>().ToList();
        wrong.AddRange(request.Outputs.Where(static o => ChallengeRoundShape.Covers(o.Key)).Select(static o => ChallengeRoundShape.Violation(o.Value.Span)).OfType<string>());   // #1102: one round, not the challenge log
        wrong.AddRange(request.Outputs.Where(static o => ReportFactSchemas.Covers(o.Key)).OrderBy(static o => o.Key.Value, StringComparer.Ordinal)
            .SelectMany(static o => ReportFactSchemas.Violations(o.Key, o.Value.Span)));   // #1112: a field the schema forbids is the writer's to fix, not a new drafter's
        if (wrong.Count == 0)
        {
            return null;
        }

        var said = string.Join("; ", wrong);
        if (_refused.AddOrUpdate((view.Address.Value, request.Run.Value), 1, static (_, count) => count + 1) == 1)
        {
            return WorkflowErrors.MalformedReport.With($"{said}. This is your one retry: fix it and run the same command again");
        }

        if (view.AgentRun?.Launch is { } launch && LaunchId.TryFrom(launch, out var id))
        {
            refusals.Record(id, said);
            _ = await launcher.Stop(id, cancellationToken).ConfigureAwait(false);   // the session ends, the watch reports 'exit', and the Step fails the way a run that never reported does
        }

        return WorkflowErrors.MalformedReport.With($"{said}. This is the second bad report of run {request.Run.Value}, so its session is ended and the Step fails");
    }
}
