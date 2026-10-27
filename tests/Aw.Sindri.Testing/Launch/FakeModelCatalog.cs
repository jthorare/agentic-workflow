using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Testing.Launch;

/// <summary>An <see cref="IModelCatalog"/> in memory: each agent offers the models a test gave it with <see cref="With"/>, and every other agent offers none.</summary>
public sealed class FakeModelCatalog : IModelCatalog
{
    private readonly Dictionary<AgentId, IReadOnlyList<ModelFacts>> _models = [];

    /// <summary>The models <paramref name="agent"/> offers from now on, in the order given.</summary>
    /// <param name="agent">The agent.</param>
    /// <param name="models">Its models.</param>
    /// <returns>This catalog.</returns>
    public FakeModelCatalog With(AgentId agent, params ModelFacts[] models)
    {
        ArgumentNullException.ThrowIfNull(models);
        _models[agent] = [.. models];
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<ModelFacts>> ModelsOf(AgentId agent, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_models.TryGetValue(agent, out var models) ? models : []);
}
