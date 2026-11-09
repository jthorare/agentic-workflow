using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Features.GetConnection;

/// <summary>Reads one connection. A bad name is SND-CONN-008, an unknown one SND-CONN-001.</summary>
/// <param name="store">The connections.</param>
/// <param name="outcomes">The last failed sign-in per name: a name with no row but a failed sign-in answers that failure instead of SND-CONN-001.</param>
internal sealed class GetConnectionService(IConnectionStore store, SignInOutcomes outcomes) : IService<GetConnectionRequest, GetConnectionResponse>
{
    private readonly IConnectionStore _store = store;

    public async ValueTask<Result<GetConnectionResponse>> Handle(GetConnectionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = ConnectionName.Parse(request.Name);
        if (name.IsError)
        {
            return name.Error;
        }

        var found = await _store.Get(name.Value, cancellationToken).ConfigureAwait(false);
        if (found.IsSuccess)
        {
            // why: a sign-in again that failed leaves the row as it was, so a caller that waits for it would otherwise see the old state until it gave up
            if (found.Value.Status != ConnectionStatus.Connected && outcomes.Last(name.Value.ToString()) is { } failedAgain)
            {
                return failedAgain;
            }

            // why: a connected row keeps working on its old tokens after a failed sign-in again, so the failure goes beside the row instead of replacing it
            var failure = outcomes.LastFailure(name.Value.ToString()) is { } f ? new SignInFailureView(f.Error.Code, f.At) : null;
            return new GetConnectionResponse(ConnectionView.From(found.Value), failure);
        }

        // why: a sign-in that failed left no row, so the caller that waits for it (the CLI, an agent) would otherwise see "not found" until it gave up
        return found.Error.Code == SindriErrors.ConnectionNotFound.Code && outcomes.Last(name.Value.ToString()) is { } failed ? failed : ConnectionFailures.Safe(found.Error);
    }
}
