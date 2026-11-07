using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Features.ReviewListFacts;

/// <summary><see cref="IReviewRepos"/> from the approved profile's checked Context settings (<see cref="ReviewScopes"/>).</summary>
/// <param name="approvals">The approved profile.</param>
/// <param name="callers">The current caller.</param>
/// <param name="engine">Context's facade, for the settings check.</param>
internal sealed class ContextReviewRepos(IApprovedProfile approvals, ICallerContextAccessor callers, IContextEngine engine) : IReviewRepos
{
    public async ValueTask<Result<IReadOnlyList<string>>> Synced(CancellationToken cancellationToken)
    {
        var read = await ReviewScopes.Read(approvals, callers, engine, cancellationToken).ConfigureAwait(false);
        return read.IsError ? read.Error : Results.Success<IReadOnlyList<string>>(read.Value.Settings.GitHub?.Repos ?? []);
    }
}
