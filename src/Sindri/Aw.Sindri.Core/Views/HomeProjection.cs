using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Views;

/// <summary>Folds the 'home.*' events into the 'home-rows' and 'home-links' views (home spec §3.1, §8): a row per object, a pure fold a 'views rebuild' reproduces.</summary>
/// <param name="events">Reads the payloads through the package's AwJson options.</param>
internal sealed class HomeProjection(EventFactory events) : IProjection
{
    public IReadOnlyList<string> Views => [ViewNames.HomeRows, ViewNames.HomeLinks];

    public bool Handles(string eventType) => eventType.StartsWith("home.", StringComparison.Ordinal);

    public ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        _ = events;
        throw new NotSupportedException("The Home fold is not written yet.");
    }
}
