using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.ValidateProfile;

internal sealed class ValidateProfileService(IProfileSource live, IApprovals approvals, ILedgerStore ledger, EventFactory events, IContextEngine context, ModelKeyChecks? models = null, IAgentCatalog? agents = null)
    : IService<ValidateProfileRequest, ValidateProfileResponse>
{
    public async ValueTask<Result<ValidateProfileResponse>> Handle(ValidateProfileRequest request, CancellationToken cancellationToken)
    {
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error;
        }

        var checkedContext = Checked(loaded.Value.Profile);
        if (checkedContext.IsError)
        {
            return checkedContext.Error;   // SND-CONTEXT-001 with its key path (context spec §4)
        }

        if (await ModelProblem(loaded.Value.Profile, cancellationToken).ConfigureAwait(false) is { } badModel)
        {
            return badModel;   // SND-MODEL-001 with the nearest id (model-pick spec §8)
        }

        var saved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (saved.IsError)
        {
            return saved.Error;   // an approval that can't be read isn't "not approved" either (doctor fails on it too)
        }

        var recorded = RecordedApproval.From(await ledger.Latest(EventTypes.ProfileApproved, cancellationToken).ConfigureAwait(false), events);
        if (recorded.IsError)
        {
            return recorded.Error;   // a ledger that can't be read isn't "not approved"
        }

        var approved = ProfileRules.IsApproved(saved.Value?.Hash, recorded.Value?.Hash, loaded.Value.Hash);
        var matches = models is null ? [] : await models.PinMatches(loaded.Value.Profile, cancellationToken).ConfigureAwait(false);
        return new ValidateProfileResponse(loaded.Value.Hash, loaded.Value.Source, approved) { Matches = matches.Count == 0 ? null : matches, Warnings = Warnings(loaded.Value.Profile, checkedContext.Value) };
    }

    // The agent target's line, then the context section's (the old 'context.conceptScan.models' key beside 'context.models.names').
    private List<string>? Warnings(SindriProfile profile, ContextSettings settings)
    {
        List<string> lines = [.. agents is not null && AgentTargetWarning.For(profile.Workflow?.Agent?.Target, agents) is { } line ? [line] : Array.Empty<string>(), .. settings.Models.Warnings];
        return lines.Count == 0 ? null : lines;
    }

    private async ValueTask<Error?> ModelProblem(SindriProfile profile, CancellationToken cancellationToken) =>
        models is null ? null : await models.Models(profile, cancellationToken).ConfigureAwait(false);

    /// <summary>The context section checked by the engine, and its 'repos' against the profile's repos (SND-CONTEXT-001).</summary>
    private Result<ContextSettings> Checked(SindriProfile profile) =>
        context.CheckSettings(profile.Context, [.. (profile.Repos ?? []).Select(r => new ProfileRepoEntry(r.Name, r.Path))]);
}
