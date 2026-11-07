namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>The review page's header reset and login prompt (<see cref="ReviewPageViewModel"/>, continued; review page spec §6.3, §16).</summary>
internal sealed partial class ReviewPageViewModel
{
    /// <summary>Saves the login in the prompt's field; when it is in effect the page is read again.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>When the answer is shown.</returns>
    internal async Task SaveLogin(CancellationToken cancellationToken)
    {
        if (_url is { } url && await Login.Save(url, cancellationToken).ConfigureAwait(true))
        {
            ShowLoading();
            await Read(cancellationToken).ConfigureAwait(true);
        }
    }

    // the previous PR's header must not show over the next PR's loading page; the PR's GitHub link comes from repo and number, so every refusal can offer it
    private void ClearHeader()
    {
        (Title, Author, HeadText, Branch, Ref, SettingsText) = (string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        PrUrl = _repo.Length > 0 ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"https://github.com/{_repo}/pull/{_number}") : string.Empty;
        (ReviewUrl, OwnPrText) = (string.Empty, string.Empty);
        (IssueKeys, IsDraft, IsYours) = ([], false, false);
        (OfflineText, MovingText) = (null, null);
        (ApproveEnabled, RequestChangesEnabled) = (false, false);
        FixTarget = null;
    }
}
