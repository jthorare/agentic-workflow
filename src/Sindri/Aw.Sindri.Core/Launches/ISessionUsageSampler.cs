namespace Aw.Sindri.Core.Launches;

/// <summary>Samples what the live launched sessions have used (SCR E-28): one private 'session.usage' per session whose tokens changed since its last sample.</summary>
public interface ISessionUsageSampler
{
    /// <summary>Takes one sample of every live launch that has an agent session id.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>How many 'session.usage' events it appended.</returns>
    public ValueTask<int> Sample(CancellationToken cancellationToken);
}
