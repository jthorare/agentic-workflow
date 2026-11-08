using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Launches;

/// <summary>Looks up the instance, Step and attempt of the Sindri launches sessions belong to, so a session row says "on &lt;instance&gt; · &lt;state&gt;" with no extra call.</summary>
public static class LaunchSteps
{
    /// <summary>The step of each launch the sessions name. A launch the 'launches' view does not hold, or cannot read, has no entry.</summary>
    /// <param name="launches">The 'launches' view.</param>
    /// <param name="ids">The launches the sessions about to be served name.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The steps by launch id; a launch the view cannot read is left out, never an error.</returns>
    public static async ValueTask<IReadOnlyDictionary<LaunchId, LaunchStep>> Of(ILaunchViews launches, IEnumerable<LaunchId> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(launches);
        ArgumentNullException.ThrowIfNull(ids);
        var steps = new Dictionary<LaunchId, LaunchStep>();
        foreach (var id in ids.Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = await launches.Get(id, cancellationToken).ConfigureAwait(false);
            if (row.IsSuccess && row.Value is { } found)
            {
                steps[id] = found.Step;   // a launch that can't be read leaves its session without a step: the step is decoration on a row the collector made
            }
        }

        return steps;
    }
}
