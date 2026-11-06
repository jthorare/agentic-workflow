using Aw.Kernel;
using Aw.Sindri.Ui.Api.Reviews;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The review calls of the fake (review page spec §6): each answers the API's goldens, the same text the handler serves.</summary>
internal sealed partial class FakeSindriApi
{
    private int _reviewsCalls;

    /// <summary>GET /sindri/reviews's answer: the "some" golden by default.</summary>
    public RouteAnswer ReviewsAnswer { get; set; } = RouteAnswer.Api("reviews", "some");

    /// <summary>How many times the Reviews list was read.</summary>
    public int ReviewsCalls => Volatile.Read(ref _reviewsCalls);

    /// <summary>When set, the Reviews list read waits for it: the page stays loading until the test completes it.</summary>
    public TaskCompletionSource? ReviewsGate { get; set; }

    public async Task<Result<ReviewList>> Reviews(Uri api, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _reviewsCalls);
        if (ReviewsGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return Answer<ReviewList>(api, ReviewRoutes.List, ReviewsAnswer);
    }
}
