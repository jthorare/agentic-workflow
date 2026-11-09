namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>Where 'for_each' reads its defaults: the live profile in the app, <see cref="ForEachLimits.Defaults"/> in a test.</summary>
public interface IForEachLimitSource
{
    /// <summary>The defaults now.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The limits.</returns>
    public ValueTask<ForEachLimits> Current(CancellationToken cancellationToken);
}
