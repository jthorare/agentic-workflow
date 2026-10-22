using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>How long a watched pull request may stay quiet before 'pr.changed' reports 'idle': the approved profile's 'workflow.pr.remindAfter' (default 14d).</summary>
internal interface IPrRemindAfter
{
    /// <summary>The quiet period.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The period; the default when no profile is approved or the key is unset.</returns>
    public ValueTask<TimeSpan> Get(CancellationToken cancellationToken);
}

/// <summary><see cref="IPrRemindAfter"/> read from the approved profile.</summary>
/// <param name="profile">The approved profile.</param>
internal sealed class ProfilePrRemindAfter(IApprovedProfile profile) : IPrRemindAfter
{
    private static readonly TimeSpan _default = TimeSpan.FromDays(14);

    public async ValueTask<TimeSpan> Get(CancellationToken cancellationToken)
    {
        var latest = await profile.Latest(cancellationToken).ConfigureAwait(false);
        var text = latest.IsError ? null : latest.Value?.Profile.Workflow?.Pr?.RemindAfter;
        return text is not null && RecipeDuration.TryFrom(text, out var duration) ? duration.Span : _default;
    }
}
