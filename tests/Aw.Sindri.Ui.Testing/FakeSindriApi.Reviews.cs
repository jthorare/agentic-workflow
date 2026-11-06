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

    private readonly List<ReviewLoginSave> _loginSaves = [];
    private int _libraryCalls;

    /// <summary>POST /sindri/reviews/login's answer: the "saved" golden by default.</summary>
    public RouteAnswer ReviewLoginAnswer { get; set; } = RouteAnswer.Api("reviews-login", "saved");

    /// <summary>GET /sindri/library's answer: the "ok" golden by default.</summary>
    public RouteAnswer LibraryAnswer { get; set; } = RouteAnswer.Api("library", "ok");

    /// <summary>The logins the page asked to save, in order.</summary>
    public IReadOnlyList<ReviewLoginSave> ReviewLoginSaves { get { lock (_gate) { return [.. _loginSaves]; } } }

    /// <summary>How many times the library was read.</summary>
    public int LibraryCalls => Volatile.Read(ref _libraryCalls);

    public Task<Result<ReviewLoginSaved>> SaveReviewLogin(Uri api, ReviewLoginSave save, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _loginSaves.Add(save);
        }

        return Task.FromResult(Answer<ReviewLoginSaved>(api, ReviewRoutes.Login, ReviewLoginAnswer));
    }

    public Task<Result<LibraryView>> Library(Uri api, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _libraryCalls);
        return Task.FromResult(Answer<LibraryView>(api, ReviewRoutes.Library, LibraryAnswer));
    }

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
