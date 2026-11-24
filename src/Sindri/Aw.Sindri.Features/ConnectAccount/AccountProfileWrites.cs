using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.ConnectAccount;

/// <summary>
/// The profile side of connecting GitHub: sets `review.credential` and clears `setup.skipAccounts`, each the way `sindri profile set` does (comments and order kept, the edited text read and checked before the atomic write).
/// The first write creates the profile when there is none (first-run spec decision 5).
/// </summary>
/// <param name="writer">Plans and writes the edit.</param>
/// <param name="parser">Reads the edited text.</param>
/// <param name="initializer">Creates the default profile when there is none.</param>
/// <param name="shipped">The effect kinds a new profile allows.</param>
/// <param name="profiles">The live profile, read before removing `review.credential`.</param>
internal sealed class AccountProfileWrites(IProfileKeyWriter writer, IProfileTextParser parser, IProfileInitializer initializer, IShippedEffects shipped, IProfileSource profiles)
{
    private const string CredentialKey = "review.credential";
    private const string SkipKey = "setup.skipAccounts";

    /// <summary>Points `review.credential` at <paramref name="credential"/> and removes `setup.skipAccounts`.</summary>
    /// <param name="credential">The credential, such as `command:gh auth token`; never a token.</param>
    /// <param name="cancellationToken">Cancels the profile's creation.</param>
    /// <returns>Unit, or the writer's or parser's error.</returns>
    internal async ValueTask<Result<Unit>> GitHub(string credential, CancellationToken cancellationToken)
    {
        var plan = writer.PlanText(CredentialKey, credential);
        if (plan.IsError && string.Equals(plan.Error.Code, SindriErrors.ProfileMissing.Code, StringComparison.Ordinal))
        {
            _ = await initializer.Create(shipped.Kinds(), cancellationToken).ConfigureAwait(false);   // the first write creates the profile; a failure shows as the same missing profile just below
            plan = writer.PlanText(CredentialKey, credential);
        }

        if (Saved(plan) is { } failed)
        {
            return failed;
        }

        return Saved(writer.PlanRemove(SkipKey)) is { } skipFailed ? skipFailed : Unit.Value;
    }

    /// <summary>Removes `setup.skipAccounts`, as a device-flow connect does (it writes no `review.credential`). No profile means nothing to clear.</summary>
    /// <param name="cancellationToken">Unused: the removal is one local file edit.</param>
    /// <returns>Unit, or the writer's or parser's error.</returns>
    internal ValueTask<Result<Unit>> ClearSkip(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var plan = writer.PlanRemove(SkipKey);
        if (plan.IsError && string.Equals(plan.Error.Code, SindriErrors.ProfileMissing.Code, StringComparison.Ordinal))
        {
            return ValueTask.FromResult<Result<Unit>>(Unit.Value);
        }

        return ValueTask.FromResult<Result<Unit>>(Saved(plan) is { } failed ? failed : Unit.Value);
    }

    /// <summary>Removes `review.credential` when it is the gh command the accounts step wrote; a credential the owner set by hand is kept.</summary>
    /// <param name="cancellationToken">Cancels the profile read.</param>
    /// <returns>Unit, or the writer's or parser's error.</returns>
    internal async ValueTask<Result<Unit>> ClearGhCredential(CancellationToken cancellationToken)
    {
        var live = await profiles.Load(cancellationToken).ConfigureAwait(false);
        if (live.IsError || !string.Equals(live.Value.Profile.Review?.Credential, Core.Connections.GitHubCliCredential.Command, StringComparison.Ordinal))
        {
            return Unit.Value;   // no profile, or not the accounts step's credential
        }

        return Saved(writer.PlanRemove(CredentialKey)) is { } failed ? failed : Unit.Value;
    }

    // Has the edited text read and checked (nothing is written on a refusal), then saves it atomically.
    private Error? Saved(Result<ProfileKeyEdit> plan)
    {
        if (plan.IsError)
        {
            return plan.Error;
        }

        if (plan.Value.Changed && parser.Parse(plan.Value.Text, plan.Value.Path) is { IsError: true } parsed)
        {
            return parsed.Error;   // SND-PROFILE-002 / SND-REVIEW-001 with the key path
        }

        var saved = writer.Save(plan.Value);
        return saved.IsError ? saved.Error : null;
    }
}
