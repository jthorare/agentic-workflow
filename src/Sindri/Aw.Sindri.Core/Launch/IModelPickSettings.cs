namespace Aw.Sindri.Core.Launch;

/// <summary>The approved profile's model-pick settings (model-pick spec §8) for one agent and recipe state; an adapter reads 'workflow.agent' and the recorded consent.</summary>
public interface IModelPickSettings
{
    /// <summary>Whether the pick is on at all: the profile says 'on' and the owner approved a profile that showed the Jev notice.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True when on.</returns>
    public ValueTask<bool> PickEnabled(CancellationToken cancellationToken);

    /// <summary>The settings that apply to a step of <paramref name="agent"/> in recipe state <paramref name="state"/> (a state-scoped pin wins over the agent's).</summary>
    /// <param name="agent">The agent.</param>
    /// <param name="state">The recipe state id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The settings; <see cref="ModelPickSettings.Off"/> when the profile says nothing.</returns>
    public ValueTask<ModelPickSettings> For(AgentId agent, string state, CancellationToken cancellationToken);
}
