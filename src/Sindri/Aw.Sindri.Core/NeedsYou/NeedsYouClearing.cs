using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.NeedsYou;

/// <summary>When a needs-you row appears, stays and clears (P4 plan §2 Unit C, S5 to S7): the pure side of the 'needs_you' fold.</summary>
public static class NeedsYouClearing
{
    /// <summary>The failure row after a service run.</summary>
    /// <param name="existing">The service's failure row so far, or null.</param>
    /// <param name="service">The service's row after the run.</param>
    /// <param name="scrubber">Scrubs the text.</param>
    /// <param name="builtIn">True when the service is a built-in.</param>
    /// <returns>The row to keep (its since is the first failure's), or null when the run was ok and the row clears.</returns>
    public static Result<NeedsYouRow?> AfterRun(NeedsYouRow? existing, ServiceRow service, ITextScrubber scrubber, bool builtIn = false)
    {
        ArgumentNullException.ThrowIfNull(service);
        if (service.LastRun is not { Outcome: ServiceOutcome.Failed } last)
        {
            return Results.Success<NeedsYouRow?>(value: null);
        }

        var row = NeedsYouRows.ForFailure(service, existing?.Since ?? last.RanAt, scrubber, builtIn);
        return row.IsError ? row.Error : Results.Success<NeedsYouRow?>(row.Value);
    }

    /// <summary>The profile row after a check.</summary>
    /// <param name="existing">The profile row so far, or null.</param>
    /// <param name="profile">The profile row after the check.</param>
    /// <param name="scrubber">Scrubs the text.</param>
    /// <returns>The row to keep (its since is the first drift's), or null when the live profile matches the approved one.</returns>
    public static Result<NeedsYouRow?> AfterCheck(NeedsYouRow? existing, ProfileRow profile, ITextScrubber scrubber)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return NeedsYouRows.ForProfile(profile, existing?.Since ?? profile.CheckedAt ?? DateTimeOffset.UnixEpoch, scrubber);
    }

    /// <summary>Whether a failure row outlives a check: false when an approved profile no longer names its service (a built-in needs no naming).</summary>
    /// <param name="failure">The failure row.</param>
    /// <param name="profile">The profile row after the check.</param>
    /// <param name="builtIns">The build's built-in services, which the profile need not name.</param>
    /// <returns>True to keep the row.</returns>
    public static bool FailureStands(NeedsYouRow failure, ProfileRow profile, IReadOnlyCollection<ServiceName>? builtIns = null)
    {
        ArgumentNullException.ThrowIfNull(failure);
        ArgumentNullException.ThrowIfNull(profile);
        var named = profile.ApprovedServices.Concat(builtIns ?? []);
        return profile.Approved is null || named.Any(s => string.Equals(failure.Subject.Id.Value, s.Value, StringComparison.Ordinal));
    }
}
