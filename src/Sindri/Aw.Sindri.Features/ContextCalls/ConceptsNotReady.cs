using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// What the concepts read answers when there is no map yet: not an error but a view with no concepts whose state says why, so the top band can show it: 'building' while a map build job runs,
/// else 'unavailable' with the engine's code (SND-CONTEXT-030).
/// </summary>
/// <param name="jobs">The 'jobs' view.</param>
internal sealed class ConceptsNotReady(IJobViews jobs)
{
    private static readonly JobKind _build = JobKind.From("BuildContextMap");

    /// <summary>The view for "no map yet".</summary>
    /// <param name="refusal">The engine's SND-CONTEXT-030.</param>
    /// <param name="scope">The caller's scope (its repo, or the profile's only one, names the answer).</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A view with state 'building' or 'unavailable', no concepts, and an empty 'other'.</returns>
    internal async ValueTask<ConceptsView> Explain(Error refusal, ContextScope scope, CancellationToken cancellationToken)
    {
        var builds = await jobs.List(state: null, _build, cancellationToken).ConfigureAwait(false);
        var running = builds.IsSuccess && builds.Value.Any(static j => j.State == JobState.Running);
        var repo = scope.Repos.Count == 1 ? scope.Repos[0].Name.Value : scope.Caller.Repo?.Value;
        return new ConceptsView(1, repo, string.Empty, running ? ConceptStates.Building : ConceptStates.Unavailable, running ? null : refusal.Code, [],
            new ConceptOther(NodeKey.From(ConceptKeys.Other), "Other", new ConceptSize(0, 0, 0, 0)), [], Next: null);
    }
}
