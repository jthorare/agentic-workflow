using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Launches;

/// <summary>Samples what the live launched sessions have used (SCR E-28): one private 'session.usage' per session whose tokens changed since its last sample.</summary>
public interface ISessionUsageSampler
{
    /// <summary>Takes one sample of every live launch that has an agent session id.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>How many 'session.usage' events it appended.</returns>
    public ValueTask<int> Sample(CancellationToken cancellationToken);

    /// <summary>Takes one sample of one launch now, as a tick would: a Step that closes reads its tokens from the ledger, so the close asks for a final sample first. A launch with no readable transcript yet writes nothing and is not an error.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>How many 'session.usage' events it appended: 0 or 1.</returns>
    public ValueTask<int> Flush(LaunchId launch, CancellationToken cancellationToken);
}
