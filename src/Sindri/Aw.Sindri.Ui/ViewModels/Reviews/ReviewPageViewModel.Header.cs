namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>The review page's header reset and login prompt (<see cref="ReviewPageViewModel"/>, continued; review page spec §6.3, §16).</summary>
internal sealed partial class ReviewPageViewModel
{
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    /// <summary>"Release 1: both buttons open this PR's review box on GitHub. Your ticks are saved on this head."</summary>
    public string ActionNote => Labels.ReviewActionNote;

    /// <summary>The Reload button's words.</summary>
    public string ReloadText => Labels.ReviewReload;

    /// <summary>"← Reviews".</summary>
    public string BackText => Labels.ReviewBackButton;

    /// <summary>"Back to Reviews".</summary>
    public string BackName => Labels.ReviewBackName;

    /// <summary>"Try again".</summary>
    public string TryAgainText => Labels.ReviewTryAgain;

    /// <summary>"Open on GitHub".</summary>
    public string OpenOnGitHubText => Labels.ReviewOpenOnGitHub;

    /// <summary>"Open on GitHub ↗".</summary>
    public string OpenOnGitHubArrowText => Labels.ReviewOpenOnGitHubArrow;
#pragma warning restore CA1822, MA0041

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
