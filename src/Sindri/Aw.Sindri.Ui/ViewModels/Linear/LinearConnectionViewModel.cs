using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>
/// Settings → Connections → Linear (Linear hub spec §3, §5.1): the read key and the teams to tick, the write key, and the words for each state of them. A key is held in memory
/// for one call and cleared once it is sent; it is never put in a log, a summary or an error. An agent session is refused the connection (SND-AUTH-002) and gets no card.
/// </summary>
internal sealed class LinearConnectionViewModel : ObservableObject
{
    private readonly Func<Uri?> _url;
    private readonly ISindriApi _api;

    /// <summary>The card over the API.</summary>
    /// <param name="url">The API's base URL, or null while it isn't known.</param>
    /// <param name="api">The API.</param>
    internal LinearConnectionViewModel(Func<Uri?> url, ISindriApi api)
    {
        (_url, _api) = (url, api);
    }

#pragma warning disable CA1822, MA0041 // why: the view binds these as instance properties; the stubs read no state yet
    /// <summary>The card's copy.</summary>
    public LinearConnectionText Text { get; } = LinearConnectionText.Instance;

    /// <summary>False once the API refused this caller the connection (an agent session): the card is not drawn.</summary>
    public bool IsAvailable => throw new NotSupportedException();

    /// <summary>The line after the card's name: "Not connected", "Reading ENG · read-only" or "Reading ENG · writing ENG".</summary>
    public string Summary => throw new NotSupportedException();

    /// <summary>True when a read key is stored and Sindri is not asking for a new one.</summary>
    public bool ReadIsSet => throw new NotSupportedException();

    /// <summary>True when the read key box is drawn.</summary>
    public bool ShowReadPaste => throw new NotSupportedException();

    /// <summary>True when the "Read access: connected" line is drawn.</summary>
    public bool ShowReadSet => throw new NotSupportedException();

    /// <summary>"Read access: connected · checked 10:05".</summary>
    public string ReadAccessText => throw new NotSupportedException();

    /// <summary>The pasted read key; empty after it is sent.</summary>
    public string ReadKey { get; set; } = string.Empty;

    /// <summary>True when List my teams can be pressed: a key is typed and no call is running.</summary>
    public bool CanListTeams => throw new NotSupportedException();

    /// <summary>The team boxes of the last list.</summary>
    public IReadOnlyList<LinearTeamTickViewModel> Teams => throw new NotSupportedException();

    /// <summary>True when the team boxes are drawn.</summary>
    public bool ShowTeams => throw new NotSupportedException();

    /// <summary>True when Check and save can be pressed for the read key: a key, at least one team ticked, no call running.</summary>
    public bool CanSaveRead => throw new NotSupportedException();

    /// <summary>The line beside Check and save.</summary>
    public string SaveHint => throw new NotSupportedException();

    /// <summary>True when the write key part is drawn (read access is set).</summary>
    public bool ShowWrite => throw new NotSupportedException();

    /// <summary>True when the write key box is drawn.</summary>
    public bool ShowWritePaste => throw new NotSupportedException();

    /// <summary>True when the "Write access: connected" line is drawn.</summary>
    public bool ShowWriteSet => throw new NotSupportedException();

    /// <summary>"Write access: connected · checked 10:05".</summary>
    public string WriteAccessText => throw new NotSupportedException();

    /// <summary>The pasted write key; empty after it is sent.</summary>
    public string WriteKey { get; set; } = string.Empty;

    /// <summary>True when Check and save can be pressed for the write key.</summary>
    public bool CanCheckWrite => throw new NotSupportedException();

    /// <summary>True while Remove key's question is shown.</summary>
    public bool IsConfirmingRemove => throw new NotSupportedException();

    /// <summary>The progress line ("Looking up your teams…", "Checking…"); empty otherwise. Its region is always in the tree.</summary>
    public string StatusText => throw new NotSupportedException();

    /// <summary>The last refusal or failure; empty otherwise.</summary>
    public string ErrorText => throw new NotSupportedException();

    /// <summary>True when there is a refusal to show.</summary>
    public bool HasError => throw new NotSupportedException();

    /// <summary>The note that needs a look but is not a refusal (scope unknown, other changes waiting); empty otherwise.</summary>
    public string AttentionText => throw new NotSupportedException();

    /// <summary>True when there is a note to show.</summary>
    public bool HasAttention => throw new NotSupportedException();

    /// <summary>True when the last change waits for the profile approval, so Review changes is drawn.</summary>
    public bool HasWaiting => throw new NotSupportedException();

    /// <summary>Raised when the card should be brought into view and given focus, once it has been read.</summary>
    internal event EventHandler? RevealRequested;

    /// <summary>Raised when Review changes is pressed.</summary>
    internal event EventHandler? ReviewRequested;

    /// <summary>Reads the connection.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the card is up to date.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken) =>
        Task.FromException(new NotSupportedException(_url()?.ToString() + _api + url + cancellationToken.CanBeCanceled));

    /// <summary>Asks for the card to be shown and focused; done as soon as the next read is in (at once when the API is not known).</summary>
    internal void Reveal()
    {
        RevealRequested?.Invoke(this, EventArgs.Empty);
        throw new NotSupportedException();
    }

    /// <summary>Review changes was pressed.</summary>
    internal void RequestReview() => ReviewRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Reads the teams the pasted read key can see.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task ListTeams(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Stores the read key with the ticked teams.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task SaveRead(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Stores the write key.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task CheckWrite(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Replace key on the read line: the paste box comes back.</summary>
    internal void ReplaceRead() => throw new NotSupportedException();

    /// <summary>Replace key on the write line: the paste box comes back.</summary>
    internal void ReplaceWrite() => throw new NotSupportedException();

    /// <summary>Remove key: asks first.</summary>
    internal void AskRemove() => throw new NotSupportedException();

    /// <summary>Keep it: the question goes away.</summary>
    internal void KeepWrite() => throw new NotSupportedException();

    /// <summary>Remove, after the question: deletes the write key.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task RemoveWrite(CancellationToken cancellationToken) => throw new NotSupportedException();
#pragma warning restore CA1822, MA0041
}
