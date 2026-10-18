using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Launches;

/// <summary>What the launches view and the catalog say about a reported session id (launcher L-9): the launch's item, and whether its entry's pattern accepts the id.</summary>
/// <param name="views">The launches view.</param>
/// <param name="catalog">The agent catalog.</param>
public sealed class ReportedIdAdmission(ILaunchViews views, IAgentCatalog catalog)
{
    /// <summary>Looks the launch up.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="id">The reported id, or null when only the item is wanted.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The item (null when the view has no row yet) and whether the id is accepted: always for a launch with no row or an entry with no pattern.</returns>
    public async ValueTask<AdmittedLaunch> Admit(LaunchId launch, SessionId? id, CancellationToken cancellationToken)
    {
        var read = await views.Get(launch, cancellationToken).ConfigureAwait(false);
        if (read.IsError || read.Value is not { } row)
        {
            return new AdmittedLaunch(Item: null, Accepted: true);
        }

        var pattern = catalog.Resolve(row.Target)?.SessionIdPattern;
        return new AdmittedLaunch(row.Item.Value, id is not { } reported || SessionIdCapture.Accepts(pattern, reported));
    }
}
