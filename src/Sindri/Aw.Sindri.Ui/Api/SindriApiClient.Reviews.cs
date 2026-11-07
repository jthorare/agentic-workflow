using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api.Reviews;

namespace Aw.Sindri.Ui.Api;

/// <summary>The review calls (<see cref="SindriApiClient"/>, continued; review page spec §6).</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<ReviewList>> Reviews(Uri api, CancellationToken cancellationToken) =>
        Get<ReviewList>(api, ReviewRoutes.List, cancellationToken, view: true);

    /// <inheritdoc />
    public Task<Result<ReviewLoginSaved>> SaveReviewLogin(Uri api, ReviewLoginSave save, CancellationToken cancellationToken) =>
        Call<ReviewLoginSaved>(api, HttpMethod.Post, ReviewRoutes.Login, cancellationToken, new CallOptions(Content: AwJson.Serialize(save, Json)));

    /// <inheritdoc />
    public Task<Result<LibraryView>> Library(Uri api, CancellationToken cancellationToken) =>
        Get<LibraryView>(api, ReviewRoutes.Library, cancellationToken, view: true);

    /// <inheritdoc />
    public Task<Result<ReviewPage>> ReadReviewPage(Uri api, string repo, int number, CancellationToken cancellationToken) =>
        Get<ReviewPage>(api, ReviewRoutes.Page(repo, number), cancellationToken, view: true);

    /// <inheritdoc />
    public Task<Result<ReviewFixSent>> SendReviewFix(Uri api, string repo, int number, ReviewFixSend send, CancellationToken cancellationToken) =>
        Call<ReviewFixSent>(api, HttpMethod.Post, ReviewRoutes.Fix(repo, number), cancellationToken, new CallOptions(Content: AwJson.Serialize(send, Json)));

    /// <inheritdoc />
    public Task<Result<ReviewSettingsRead>> GetReviewSettings(Uri api, string project, CancellationToken cancellationToken) =>
        Get<ReviewSettingsRead>(api, ReviewRoutes.SettingsOf(project), cancellationToken);

    /// <inheritdoc />
    public Task<Result<ReviewSettingsSaved>> SaveReviewSettings(Uri api, ReviewSettingsSave request, CancellationToken cancellationToken) =>
        Call<ReviewSettingsSaved>(api, HttpMethod.Put, ReviewRoutes.Settings, cancellationToken, new CallOptions(Content: AwJson.Serialize(request, Json)));

    /// <inheritdoc />
    public Task<Result<LibraryRootChanged>> AddLibraryRoot(Uri api, LibraryRootAdd add, CancellationToken cancellationToken) =>
        Call<LibraryRootChanged>(api, HttpMethod.Post, ReviewRoutes.LibraryRoots, cancellationToken, new CallOptions(Content: AwJson.Serialize(add, Json)));

    /// <inheritdoc />
    public Task<Result<LibraryRootChanged>> RemoveLibraryRoot(Uri api, string name, bool approve, CancellationToken cancellationToken) =>
        Call<LibraryRootChanged>(api, HttpMethod.Delete, ReviewRoutes.LibraryRoot(name, approve), cancellationToken);

    /// <inheritdoc />
    public Task<Result<LibraryRootChanged>> SetLibraryAw(Uri api, LibraryAwSwitch change, CancellationToken cancellationToken) =>
        Call<LibraryRootChanged>(api, HttpMethod.Post, ReviewRoutes.LibraryAw, cancellationToken, new CallOptions(Content: AwJson.Serialize(change, Json)));
}
