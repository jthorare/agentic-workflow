using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Testing.Launch;

/// <summary>An <see cref="IModelPickSettings"/> in memory: every agent and state gets the settings a test gave it.</summary>
public sealed class FakeModelPickSettings : IModelPickSettings
{
    /// <summary>The settings every call answers; the pick is off until a test sets them.</summary>
    public ModelPickSettings Settings { get; set; } = ModelPickSettings.Off;

    /// <inheritdoc/>
    public ValueTask<bool> PickEnabled(CancellationToken cancellationToken) => ValueTask.FromResult(Settings.PickOn);

    /// <inheritdoc/>
    public ValueTask<ModelPickSettings> For(AgentId agent, string state, CancellationToken cancellationToken) => ValueTask.FromResult(Settings);
}
