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

    private readonly List<(string Repo, int Number)> _pageReads = [];
    private readonly List<ReviewFixSend> _fixSends = [];

    /// <summary>GET /sindri/reviews/{owner}/{repo}/{number}'s answer: the "full" golden by default.</summary>
    public RouteAnswer ReviewPageAnswer { get; set; } = RouteAnswer.Api("reviews-pr", "full");

    /// <summary>When set, the page read waits for it: the page stays loading until the test completes it.</summary>
    public TaskCompletionSource? ReviewPageGate { get; set; }

    /// <summary>The PRs whose page was read, in order.</summary>
    public IReadOnlyList<(string Repo, int Number)> ReviewPageReads { get { lock (_gate) { return [.. _pageReads]; } } }

    /// <summary>POST .../fix's answer: a ReviewFixSent body; the babysitter took it by default.</summary>
    public RouteAnswer ReviewFixAnswer { get; set; } = new(System.Net.HttpStatusCode.OK, ReviewFixBodies.Babysitter);

    /// <summary>When set, the fix send waits for it: the sheet stays on Sending until the test completes it.</summary>
    public TaskCompletionSource? ReviewFixGate { get; set; }

    /// <summary>The fix tasks the page asked to send, in order.</summary>
    public IReadOnlyList<ReviewFixSend> ReviewFixSends { get { lock (_gate) { return [.. _fixSends]; } } }

    public async Task<Result<ReviewPage>> ReadReviewPage(Uri api, string repo, int number, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _pageReads.Add((repo, number));
        }

        if (ReviewPageGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return Answer<ReviewPage>(api, ReviewRoutes.Page(repo, number), ReviewPageAnswer);
    }

    public async Task<Result<ReviewFixSent>> SendReviewFix(Uri api, string repo, int number, ReviewFixSend send, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _fixSends.Add(send);
        }

        if (ReviewFixGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return Answer<ReviewFixSent>(api, ReviewRoutes.Fix(repo, number), ReviewFixAnswer);
    }
}
