using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Lease;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Features.CheckProfile;

/// <summary>
/// Records a profile check now, as the caller 'service:profile-watch'. Sindri's own profile writes call it right after the file changed, so the 'profile' view's live hash is the hash the write produced before
/// the write's job settles; the profile watch calls it on its tick. Without the lease (the CLI process, or serve before it holds one) nothing is recorded. One gate serves both callers: two checks read the same
/// latest 'profile.checked' and append with the same idempotency key, so racing ones would drop the later.
/// </summary>
/// <param name="scopes">Makes the scope the check service lives in.</param>
/// <param name="held">The grant this process holds.</param>
/// <param name="callers">Names the caller.</param>
/// <param name="log">Where a failed check is reported.</param>
internal sealed partial class ProfileRecheck(IServiceScopeFactory scopes, HeldLease held, ICallerContextAccessor callers, ILogger<ProfileRecheck> log) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _problem;

    /// <summary>Checks the live profile against the approved one and records 'profile.checked' when the pair changed. Never throws for a failed check: the write already happened and the watch retries.</summary>
    /// <param name="cancellationToken">Cancels the wait for the gate and the check.</param>
    /// <returns>When the check ended (or was skipped).</returns>
    public async ValueTask Now(CancellationToken cancellationToken)
    {
        if (held.Current is not { } grant)
        {
            return;
        }

        try
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;   // shutting down: the next start checks again
        }

        try
        {
            Report(await Check(grant, cancellationToken).ConfigureAwait(false));
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private async Task<Result<CheckProfileResponse>> Check(LeaseGrant grant, CancellationToken ct)
    {
        using var caller = callers.EnterService("profile-watch");
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            try
            {
                var service = scope.ServiceProvider.GetRequiredService<IService<CheckProfileRequest, CheckProfileResponse>>();
                return await service.Handle(new CheckProfileRequest(grant), ct).ConfigureAwait(false);
            }
            catch (Exception e) when (!ct.IsCancellationRequested)   // boundary: one failed check must not fail the write or stop the watch
            {
                return Core.SindriErrors.ServiceFailed.With(e.Message);
            }
        }
    }

    private void Report(Result<CheckProfileResponse> checkedNow)
    {
        if (checkedNow.IsSuccess)
        {
            _problem = null;
            if (checkedNow.Value.Sequence is { } sequence)
            {
                LogRecorded(log, sequence);
            }

            return;
        }

        if (!string.Equals(_problem, checkedNow.Error.Message, StringComparison.Ordinal))
        {
            _problem = checkedNow.Error.Message;
            LogFailed(log, checkedNow.Error.Code, checkedNow.Error.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "profile.checked recorded at sequence {Sequence}")]
    private static partial void LogRecorded(ILogger logger, long sequence);

    [LoggerMessage(Level = LogLevel.Warning, Message = "SND-SCHED-001 the profile watch couldn't check the profile: {Code} {Detail}")]
    private static partial void LogFailed(ILogger logger, string code, string detail);
}
