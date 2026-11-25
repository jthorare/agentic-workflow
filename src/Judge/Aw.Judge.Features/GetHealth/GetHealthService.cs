using Aw.Judge.Core.Health;
using Aw.Judge.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.GetHealth;

internal sealed class GetHealthService(IDecisionLog decisions, TimeProvider clock) : IService<GetHealthRequest, GetHealthResponse>
{
    public async ValueTask<Result<GetHealthResponse>> Handle(GetHealthRequest request, CancellationToken cancellationToken)
    {
        var failures = await decisions.CountFailuresSince(clock.GetUtcNow() - JudgeHealth.FailureWindow, cancellationToken).ConfigureAwait(false);
        if (failures.IsError)
        {
            return failures.Error;
        }

        var lastAttempts = await decisions.LastAttemptOutcomes(JudgeHealth.DownStreak, cancellationToken).ConfigureAwait(false);
        if (lastAttempts.IsError)
        {
            return lastAttempts.Error;
        }

        var newest = await decisions.LatestFailuresSince(clock.GetUtcNow() - JudgeHealth.FailureWindow, 1, cancellationToken).ConfigureAwait(false);
        return newest.IsError
            ? newest.Error
            : new GetHealthResponse(JudgeHealth.Assess(failures.Value, lastAttempts.Value), failures.Value, newest.Value.Count == 0 ? null : newest.Value[0]);
    }
}
