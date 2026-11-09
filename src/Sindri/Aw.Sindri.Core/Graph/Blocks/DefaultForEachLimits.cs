namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The source that always answers <see cref="ForEachLimits.Defaults"/>.</summary>
public sealed class DefaultForEachLimits : IForEachLimitSource
{
    /// <inheritdoc />
    public ValueTask<ForEachLimits> Current(CancellationToken cancellationToken) => ValueTask.FromResult(ForEachLimits.Defaults);
}
