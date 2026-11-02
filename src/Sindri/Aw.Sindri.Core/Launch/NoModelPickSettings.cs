namespace Aw.Sindri.Core.Launch;

/// <summary>The settings of a build with no profile adapter yet: the pick is off for every agent.</summary>
public sealed class NoModelPickSettings : IModelPickSettings
{
    /// <inheritdoc/>
    public ValueTask<bool> PickEnabled(CancellationToken cancellationToken) => ValueTask.FromResult(false);

    /// <inheritdoc/>
    public ValueTask<ModelPickSettings> For(AgentId agent, string state, CancellationToken cancellationToken) => ValueTask.FromResult(ModelPickSettings.Off);
}
