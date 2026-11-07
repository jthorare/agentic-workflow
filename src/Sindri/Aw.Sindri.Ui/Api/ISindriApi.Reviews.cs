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

    /// <summary>GET /sindri/reviews/settings?project= (review page spec §7, Ph9): one project's review settings, as written and as resolved.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="project">The project's id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The settings, SND-REVIEW-002 (an unknown project), or SND-UI-002/003.</returns>
    public Task<Result<ReviewSettingsRead>> GetReviewSettings(Uri api, string project, CancellationToken cancellationToken);

    /// <summary>PUT /sindri/reviews/settings (Ph9): writes the project's settings entry, approving it in the same click when asked and nothing else waits.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="request">The project, repos, entry name, entry and whether to approve.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Whether it took effect, SND-REVIEW-001/002 (400, the offending key in the error's <c language="text">field</c>), SND-AUTH-002 (a session), or SND-UI-002/003.</returns>
    public Task<Result<ReviewSettingsSaved>> SaveReviewSettings(Uri api, ReviewSettingsSave request, CancellationToken cancellationToken);
}
