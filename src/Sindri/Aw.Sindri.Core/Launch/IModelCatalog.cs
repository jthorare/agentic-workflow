namespace Aw.Sindri.Core.Launch;

/// <summary>The models each agent offers, as the launch probe read them (model-pick spec §3).</summary>
public interface IModelCatalog
{
    /// <summary>The models of <paramref name="agent"/>, in the order the source gave them.</summary>
    /// <param name="agent">The agent.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The models; empty when the agent offers none or its list could not be read (never null, never an invented list).</returns>
    public ValueTask<IReadOnlyList<ModelFacts>> ModelsOf(AgentId agent, CancellationToken cancellationToken);
}
