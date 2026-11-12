using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.SkipSetupStep;

/// <summary>
/// Sets `setup.skipAccounts` or `setup.skipFirstWorkflow` to true in the live profile the way `sindri profile set` does (comments and order kept, the edited text read and checked before the atomic write), and removes the key
/// to undo it. The first write creates the profile when there is none (first-run spec decision 5). Any caller may skip: a skip is undone in one click, and the profile is the owner's own file. Writes no ledger event.
/// </summary>
/// <param name="writer">Plans and writes the edit.</param>
/// <param name="parser">Reads the edited text.</param>
/// <param name="initializer">Creates the default profile when there is none.</param>
/// <param name="shipped">The effect kinds a new profile allows.</param>
internal sealed class SkipSetupStepService(IProfileKeyWriter writer, IProfileTextParser parser, IProfileInitializer initializer, IShippedEffects shipped)
    : IService<SkipSetupStepRequest, SkipSetupStepResponse>
{
    private const string AccountsKey = "setup.skipAccounts";
    private const string FirstWorkflowKey = "setup.skipFirstWorkflow";

    public async ValueTask<Result<SkipSetupStepResponse>> Handle(SkipSetupStepRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var key = request.Step switch
        {
            SetupStepId.Sources => AccountsKey,
            SetupStepId.FirstWorkflow => FirstWorkflowKey,
            SetupStepId.Repo or SetupStepId.Profile or SetupStepId.Mcp or SetupStepId.Map or SetupStepId.Serve or SetupStepId.Agent => null,
            _ => null,
        };
        if (key is null)
        {
            return SindriErrors.InvalidInput.With($"The {SetupStepChanges.Word(request.Step)} step can't be skipped: only sources and first-workflow can (fix: sindri setup skip accounts|first-workflow)");
        }

        var plan = Plan(key, request.Skipped);
        if (plan.IsError && IsMissing(plan.Error))
        {
            if (!request.Skipped)
            {
                return new SkipSetupStepResponse(request.Step, Skipped: false, Changed: false);   // no profile, so nothing to undo
            }

            _ = await initializer.Create(shipped.Kinds(), cancellationToken).ConfigureAwait(false);   // the first write creates the profile; a failure shows as the same missing profile just below
            plan = Plan(key, skipped: true);
        }

        if (plan.IsError)
        {
            return plan.Error;
        }

        if (plan.Value.Changed && parser.Parse(plan.Value.Text, plan.Value.Path) is { IsError: true } parsed)
        {
            return parsed.Error;   // SND-PROFILE-002 with the key path; nothing was written
        }

        var saved = writer.Save(plan.Value);
        return saved.IsError ? saved.Error : new SkipSetupStepResponse(request.Step, request.Skipped, plan.Value.Changed);
    }

    private Result<ProfileKeyEdit> Plan(string key, bool skipped) => skipped ? writer.Plan(key, "true") : writer.PlanRemove(key);

    private static bool IsMissing(Error error) => string.Equals(error.Code, SindriErrors.ProfileMissing.Code, StringComparison.Ordinal);
}
