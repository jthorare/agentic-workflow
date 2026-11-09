using System.Globalization;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Memory;

namespace Aw.Sindri.Ui.ViewModels.Memory;

/// <summary>
/// One memory store: its name, description and fact count, and (open) the search, the write form and the forget actions. The page offers what the
/// memory MCP tools do and no more: search, write and three forms of forget (one fact, a tag, the expired). A store can't be deleted here and a fact can't be edited.
/// </summary>
internal sealed class MemoryStoreRowViewModel : ObservableObject
{
    private readonly ISindriApi _api;
    private readonly Func<Uri?> _url;

    /// <summary>The row over a served store.</summary>
    /// <param name="store">The store.</param>
    /// <param name="api">The API.</param>
    /// <param name="url">The API's base URL, or null while it isn't known.</param>
    /// <param name="clock">The clock expiry is read against.</param>
    internal MemoryStoreRowViewModel(MemoryView store, ISindriApi api, Func<Uri?> url, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(store);
        (_api, _url, Name, FactCount) = (api, url, store.Name, store.Facts);
        Description = store.Description ?? string.Empty;
        Search = new MemorySearchViewModel(api, url, store.Name, clock, ForgetFact, ForgetTag);
    }

    /// <summary>The store's name.</summary>
    public string Name { get; }

    /// <summary>What the store is for; empty when it has no description.</summary>
    public string Description { get; private set; }

    /// <summary>True when the store has a description.</summary>
    public bool HasDescription => Description.Length > 0;

    /// <summary>How many facts the store holds.</summary>
    internal int FactCount { get; private set; }

    /// <summary>"3 facts".</summary>
    public string FactsText => Labels.Facts(FactCount);

    /// <summary>The row's accessible name.</summary>
    public string RowName => Name + ", " + FactsText;

    /// <summary>True while the store is open.</summary>
    public bool IsOpen { get; set { Set(ref field, value); OnChanged(nameof(ToggleText)); } }

    /// <summary>"Open" or "Close".</summary>
    public string ToggleText => IsOpen ? Labels.MemoryClose : Labels.MemoryOpen;

    /// <summary>The toggle's accessible name: it says which store.</summary>
    public string ToggleName => ToggleText + " " + Name;

    /// <summary>The store's search.</summary>
    public MemorySearchViewModel Search { get; }

    /// <summary>Updates the served fields of a store already listed.</summary>
    /// <param name="store">The store as served.</param>
    internal void Update(MemoryView store)
    {
        ArgumentNullException.ThrowIfNull(store);
        Description = store.Description ?? string.Empty;
        FactCount = store.Facts;
        OnChanged(nameof(Description));
        OnChanged(nameof(HasDescription));
        OnChanged(nameof(FactsText));
        OnChanged(nameof(RowName));
    }

    // ---- write a fact (memory_write) ----

    /// <summary>The fact being written.</summary>
    public string WriteText { get; set => Set(ref field, value ?? string.Empty); } = string.Empty;

    /// <summary>The tags being written, separated by commas.</summary>
    public string WriteTags { get; set => Set(ref field, value ?? string.Empty); } = string.Empty;

    /// <summary>The expiry being written, as yyyy-mm-dd; empty for none.</summary>
    public string WriteExpires { get; set => Set(ref field, value ?? string.Empty); } = string.Empty;

    /// <summary>What the last write said: "Added." or "Refreshed: …"; or its error.</summary>
    public string WriteResult { get; private set { Set(ref field, value); OnChanged(nameof(HasWriteResult)); } } = string.Empty;

    /// <summary>True when there is something to say about the last write.</summary>
    public bool HasWriteResult => WriteResult.Length > 0;

    /// <summary>The write form's words.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string WriteHeading => Labels.MemoryWriteHeading;

    /// <summary>The fact field's accessible name.</summary>
    public string FactFieldName => Labels.MemoryFactField;

    /// <summary>The tags field's accessible name.</summary>
    public string TagsFieldName => Labels.MemoryTagsField;

    /// <summary>The expiry field's accessible name.</summary>
    public string ExpiresFieldName => Labels.MemoryExpiresField;

    /// <summary>The write action's words.</summary>
    public string WriteActionText => Labels.MemoryWrite;

    /// <summary>The forget-expired action's words.</summary>
    public string ForgetExpiredText => Labels.MemoryForgetExpired;
#pragma warning restore CA1822, MA0041

    /// <summary>Writes the fact in the form. The form clears only after the API answers.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task Write(CancellationToken cancellationToken)
    {
        var text = WriteText.Trim();
        if (text.Length == 0)
        {
            WriteResult = Labels.MemoryFactNeeded;
            return;
        }

        if (!TryExpiry(out var expires))
        {
            WriteResult = Labels.MemoryExpiresInvalid;
            return;
        }

        if (_url() is not { } url)
        {
            WriteResult = Labels.MemoryWriteFailed(SindriUiErrors.Unreachable.With("Sindri isn't reachable yet"));
            return;
        }

        var tags = WriteTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var answer = await _api.WriteMemoryFact(url, Name, new WriteMemoryFactBody(text, tags, expires, Provenance: null), cancellationToken).ConfigureAwait(true);
        if (!answer.IsSuccess)
        {
            WriteResult = Labels.MemoryWriteFailed(answer.Error);   // the form keeps what was typed
            return;
        }

        var refreshed = string.Equals(answer.Value.Action, "refreshed", StringComparison.Ordinal);
        WriteResult = refreshed ? Labels.MemoryFactRefreshed : Labels.MemoryFactAdded;
        if (!refreshed)
        {
            SetFactCount(FactCount + 1);
        }

        (WriteText, WriteTags, WriteExpires) = (string.Empty, string.Empty, string.Empty);
    }

    // An empty field is no expiry; anything else has to be yyyy-mm-dd.
    private bool TryExpiry(out DateTimeOffset? expires)
    {
        expires = null;
        var typed = WriteExpires.Trim();
        if (typed.Length == 0)
        {
            return true;
        }

        var ok = DateTimeOffset.TryParseExact(typed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var at);
        expires = ok ? at : null;
        return ok;
    }

    // ---- forget (memory_forget): one fact, every fact with a tag, every expired fact ----

    /// <summary>What the last forget said ("Forgot 2 facts.", "Nothing was forgotten.") or its error; the rows stay on an error.</summary>
    public string ForgetResult { get; private set { Set(ref field, value); OnChanged(nameof(HasForgetResult)); } } = string.Empty;

    /// <summary>True when there is something to say about the last forget.</summary>
    public bool HasForgetResult => ForgetResult.Length > 0;

    /// <summary>Forgets one fact.</summary>
    /// <param name="factId">The fact's id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task ForgetFact(string factId, CancellationToken cancellationToken) =>
        Forget(new MemoryForget(Name, FactId: factId), r => string.Equals(r.Key, factId, StringComparison.Ordinal), cancellationToken);

    /// <summary>Forgets every fact carrying a tag.</summary>
    /// <param name="tag">The tag.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task ForgetTag(string tag, CancellationToken cancellationToken) =>
        Forget(new MemoryForget(Name, Tag: tag), r => r.Fact.Tags.Contains(tag, StringComparer.Ordinal), cancellationToken);

    /// <summary>Forgets every expired fact.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task ForgetExpired(CancellationToken cancellationToken) =>
        Forget(new MemoryForget(Name, Expired: true), Search.IsExpired, cancellationToken);

    // The rows go away only after the API answers with Deleted >= 1; a failure or a zero keeps them and says so.
    private async Task Forget(MemoryForget forget, Func<MemoryFactRowViewModel, bool> rows, CancellationToken cancellationToken)
    {
        if (_url() is not { } url)
        {
            ForgetResult = Labels.MemoryForgetFailed(SindriUiErrors.Unreachable.With("Sindri isn't reachable yet"));
            return;
        }

        var answer = await _api.ForgetMemoryFacts(url, forget, cancellationToken).ConfigureAwait(true);
        if (!answer.IsSuccess)
        {
            ForgetResult = Labels.MemoryForgetFailed(answer.Error);
            return;
        }

        var deleted = answer.Value.Deleted;
        if (deleted < 1)
        {
            ForgetResult = Labels.MemoryForgotNothing;
            return;
        }

        Search.Remove(rows);
        SetFactCount(Math.Max(0, FactCount - deleted));
        ForgetResult = Labels.MemoryForgot(deleted);
    }

    private void SetFactCount(int count)
    {
        FactCount = count;
        OnChanged(nameof(FactsText));
        OnChanged(nameof(RowName));
    }
}
