using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.RestartServe;

/// <summary>The snapshot's launched sessions, looked up in the 'launches' view for their instance.</summary>
internal sealed class OpenRuns(ISessionSnapshot snapshot, ILaunchViews launches) : IOpenRuns
{
    public ValueTask<IReadOnlyList<KeptRun>> Read(CancellationToken cancellationToken)
    {
        _ = (snapshot, launches);   // why: red stub; the real body uses them
        throw new NotSupportedException();
    }
}
