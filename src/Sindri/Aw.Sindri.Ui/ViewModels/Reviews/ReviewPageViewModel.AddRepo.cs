using Aw.Sindri.Ui.Api.Setup;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>The review page (<see cref="ReviewPageViewModel"/>, continued): Add this repo on the "Sindri doesn't sync this repo" page (#3065).</summary>
internal sealed partial class ReviewPageViewModel
{
    /// <summary>True when the repo is not synced and this head can ask for a folder: the state offers Add this repo (#3065).</summary>
    public bool OffersAddRepo => State == ReviewPageState.NotSynced && _folders is { IsAvailable: true };

    /// <summary>The failed add as the shared line draws it (words, Retry, the code behind Details); null otherwise.</summary>
    public FailedRead? AddRepoFailure { get; private set => Set(ref field, value); }

    /// <summary>Add this repo was pressed: asks for the repo's folder, adds it, then reads the review again. A cancelled chooser does nothing; a failed add shows <see cref="AddRepoFailure"/>.</summary>
    /// <param name="cancellationToken">Cancels the calls.</param>
    /// <returns>When the answer is shown.</returns>
    internal async Task AddRepo(CancellationToken cancellationToken)
    {
        if (_url is not { } url || _folders is not { IsAvailable: true })
        {
            return;
        }

        var path = await _folders.Pick(Labels.ReviewAddRepoChooser, cancellationToken).ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var added = await _api.AddRepo(url, new AddRepoRequest(path), cancellationToken).ConfigureAwait(true);
        if (added.IsError)
        {
            AddRepoFailure = new FailedRead(Labels.ReviewAddRepoFailed, AddRepo) { Code = added.Error.Code };
            return;
        }

        AddRepoFailure = null;
        ShowLoading();
        await Read(cancellationToken).ConfigureAwait(true);
    }
}
