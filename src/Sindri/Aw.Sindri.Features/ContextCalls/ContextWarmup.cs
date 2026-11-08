using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// Reads the current map as a person at the app does, so the view a search needs is built before anyone asks. The engine keeps a view until a
/// write moves the change stamp, so this costs nothing while the map is unchanged and builds the view again soon after a promote, a review
/// decision or an intent sync, instead of the next search paying it (about 3 s on a 24k-node map, past the app's 2 s budget).
/// </summary>
internal sealed class ContextWarmup(IApprovedProfile approvals, IContextEngine engine)
{
    // A key no map holds: the lens read loads the map and builds the view, then answers SND-CONTEXT-003, which is all a warm read needs.
    private const string NoNode = "feature:sindri-warm-read";

    /// <summary>Reads the current map once as a person at the app; false when there is no approved profile or the read failed.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Whether the view is warm.</returns>
    internal async ValueTask<bool> Warm(CancellationToken cancellationToken)
    {
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError || approved.Value is not { } loaded)
        {
            return false;
        }

        var scope = ContextScopes.For(loaded.Profile, new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Http));
        var read = await engine.Lenses(NoNode, MapRead.Current, depth: null, scope, cancellationToken).ConfigureAwait(false);
        return read.IsSuccess || read.Error.Code == ContextErrors.UnknownAnchor.Code;
    }
}
