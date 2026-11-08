using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.CheckProfile;

/// <summary>
/// Compares the live profile with the approved one and appends 'profile.checked' when the pair changed since the last
/// check (API spec C8). The last check comes from the ledger, so a restart resumes where the previous process stopped.
/// </summary>
internal sealed class CheckProfileService(
    IProfileSource live, IApprovedProfile approvals, ILedgerStore ledger, EventFactory events, ICallerContextAccessor callers, IIdGenerator ids)
    : IService<CheckProfileRequest, CheckProfileResponse>
{
    public async ValueTask<Result<CheckProfileResponse>> Handle(CheckProfileRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var latest = await ledger.Latest(EventTypes.ProfileChecked, cancellationToken).ConfigureAwait(false);
        var previous = latest.IsError ? latest.Error : Previous(latest.Value);
        if (previous.IsError)
        {
            return previous.Error;
        }

        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        var check = ProfileCheck.Decide(previous.Value, approved.Value, loaded, callers.Current?.CommandId ?? ids.New<CommandId>());
        if (check is null)
        {
            return new CheckProfileResponse(Sequence: null);
        }

        var entry = events.Entry(EventTypes.ProfileChecked, check,
            string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ProfileChecked}:{latest.Value?.Sequence ?? 0}"));
        var appended = await ledger.Append(entry, request.Grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : new CheckProfileResponse(appended.Value.Sequence);
    }

    private Result<ProfileChecked?> Previous(LedgerEvent? recorded) =>
        recorded is null ? Results.Success<ProfileChecked?>(value: null) : events.Payload<ProfileChecked>(recorded).Map(static p => (ProfileChecked?)p);
}
