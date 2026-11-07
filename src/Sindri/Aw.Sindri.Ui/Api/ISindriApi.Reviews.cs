using Aw.Kernel;
using Aw.Sindri.Ui.Api.Reviews;

namespace Aw.Sindri.Ui.Api;

/// <summary>The review reads of the API (<see cref="ISindriApi"/>, continued; review page spec §6).</summary>
internal partial interface ISindriApi
{
    /// <summary>GET /sindri/reviews (review page spec §6.1): the owner's PRs and the ones waiting on the owner, the library header and the waiting count. A view read.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The list (the login prompt while review.login is unset), the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<ReviewList>> Reviews(Uri api, CancellationToken cancellationToken);

    /// <summary>POST /sindri/reviews/login (review page spec §6.1): saves review.login from the Reviews tab's one field.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="save">The login.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The saved login, or SND-REVIEW-001 (a bad login), SND-AUTH-002 (a session), or SND-UI-002/003.</returns>
    public Task<Result<ReviewLoginSaved>> SaveReviewLogin(Uri api, ReviewLoginSave save, CancellationToken cancellationToken);

    /// <summary>GET /sindri/library (review page spec §6.1): the library errors sheet's read. A view read.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The library, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<LibraryView>> Library(Uri api, CancellationToken cancellationToken);

    /// <summary>GET /sindri/reviews/{owner}/{repo}/{number} (review page spec §6.2): one PR's page model. A view read.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="repo">owner/name.</param>
    /// <param name="number">The PR number.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The page, the API's refusal (SND-REVIEW-004, -007, -404), or SND-UI-002/003.</returns>
    public Task<Result<ReviewPage>> ReadReviewPage(Uri api, string repo, int number, CancellationToken cancellationToken);

    /// <summary>POST /sindri/reviews/{owner}/{repo}/{number}/fix (review page spec §8.2): sends a finding as a fix task.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="repo">owner/name.</param>
    /// <param name="number">The PR number.</param>
    /// <param name="send">The finding.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Where the task went (a half-done send says so in the answer), or SND-UI-002/003.</returns>
    public Task<Result<ReviewFixSent>> SendReviewFix(Uri api, string repo, int number, ReviewFixSend send, CancellationToken cancellationToken);

    /// <summary>POST /sindri/library/roots (review page spec §6.3): adds a vault to the library.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="add">The vault.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Whether it took effect, or SND-LIB-001 (a 400 whose "field" extension names name, path or deny), SND-AUTH-002 (a session), or SND-UI-002/003.</returns>
    public Task<Result<LibraryRootChanged>> AddLibraryRoot(Uri api, LibraryRootAdd add, CancellationToken cancellationToken);

    /// <summary>DELETE /sindri/library/roots/{name} (review page spec §6.3): removes a vault.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="name">The vault's name.</param>
    /// <param name="approve">True to approve the profile change when it is the only waiting one.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Whether it took effect, or SND-LIB-001 (not a vault), a not-found, SND-AUTH-002 (a session), or SND-UI-002/003.</returns>
    public Task<Result<LibraryRootChanged>> RemoveLibraryRoot(Uri api, string name, bool approve, CancellationToken cancellationToken);

    /// <summary>POST /sindri/library/aw (review page spec §6.3): turns the built-in aw folder on or off.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="change">On or off.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Whether it took effect, SND-AUTH-002 (a session), or SND-UI-002/003.</returns>
    public Task<Result<LibraryRootChanged>> SetLibraryAw(Uri api, LibraryAwSwitch change, CancellationToken cancellationToken);
}
