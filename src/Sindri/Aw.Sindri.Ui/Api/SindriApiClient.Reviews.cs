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
}
