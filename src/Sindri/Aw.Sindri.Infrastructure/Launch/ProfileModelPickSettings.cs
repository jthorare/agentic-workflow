using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// <see cref="IModelPickSettings"/> over the APPROVED profile's 'workflow.agent' block (model-pick spec 8): the pick is on only when that block says 'on', so a profile the owner has not approved
/// (and one that never showed the Jev notice) picks nothing. The pin is <see cref="ModelKeyRules.PinFor"/>'s, the cap is the agent's own entry, and the deny globs are the profile's index.denyPaths.
/// A profile that can't be read means off: no pick and nothing sent to Jev.
/// </summary>
/// <param name="approved">The approved profile.</param>
internal sealed class ProfileModelPickSettings(IApprovedProfile approved) : IModelPickSettings
{
    public async ValueTask<bool> PickEnabled(CancellationToken cancellationToken) =>
        await Agent(cancellationToken).ConfigureAwait(false) is { } agent && IsOn(agent.Settings);

    public async ValueTask<ModelPickSettings> For(AgentId agent, string state, CancellationToken cancellationToken)
    {
        if (await Agent(cancellationToken).ConfigureAwait(false) is not { } found || !IsOn(found.Settings))
        {
            return ModelPickSettings.Off;
        }

        var settings = found.Settings;
        var own = settings.Models is { } models && models.TryGetValue(agent.Value, out var listed) ? listed : null;
        var cap = settings.Cap is { } caps && caps.TryGetValue(agent.Value, out var capped) ? capped : null;
        return new ModelPickSettings(
            PickOn: true,
            Pin: ModelKeyRules.PinFor(settings, agent, state),
            Cap: cap,
            Models: own is null ? null : [.. own.List.Select(static (slug, order) => new ModelFacts(slug, slug, ModelSource.Profile, order))],
            Default: own?.Default,
            DenyPaths: found.DenyPaths);
    }

    private static bool IsOn(WorkflowAgentSettings settings) => settings.PickIsOn;

    private async ValueTask<(WorkflowAgentSettings Settings, IReadOnlyList<string>? DenyPaths)?> Agent(CancellationToken cancellationToken)
    {
        var latest = await approved.Latest(cancellationToken).ConfigureAwait(false);
        return latest.IsError || latest.Value is not { } loaded || loaded.Profile.Workflow?.Agent is not { } agent ? null : (agent, loaded.Profile.Index?.DenyPaths);
    }
}
