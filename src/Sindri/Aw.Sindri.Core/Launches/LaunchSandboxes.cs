using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Launches;

/// <summary>Looks up the sandbox of the Sindri launches sessions belong to, so a session row says "sandboxed · mem 2.1/12 GiB" with no extra call.</summary>
public static class LaunchSandboxes
{
    /// <summary>The sandbox of each sandboxed launch the sessions name. An unsandboxed launch, and one the 'launches' view cannot read, has no entry.</summary>
    /// <param name="launches">The 'launches' view.</param>
    /// <param name="reader">Reads the plan file and the cgroup.</param>
    /// <param name="ids">The launches the sessions about to be served name.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The sandboxes by launch id.</returns>
    public static async ValueTask<IReadOnlyDictionary<LaunchId, SessionSandbox>> Of(ILaunchViews launches, ISessionSandboxes reader, IEnumerable<LaunchId> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(launches);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(ids);
        var found = new Dictionary<LaunchId, SessionSandbox>();
        foreach (var id in ids.Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = await launches.Get(id, cancellationToken).ConfigureAwait(false);
            if (row.IsSuccess && row.Value is { Sandbox: { } summary })
            {
                found[id] = await reader.Read(id, summary, cancellationToken).ConfigureAwait(false);   // a launch the view can't read leaves its session without a sandbox: it is decoration on a row the collector made
            }
        }

        return found;
    }
}
