using Aw.Sindri.Ui.Api.Connections;
using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Connections;

/// <summary>
/// One connection on the Connections page and in its sheet. It is kept by its name across reads and updated in place, so a control with focus keeps it. It maps what
/// the API served to words and runs the row's actions: Test, Sign in again (oauth and mcp only; a key connection is refused, SND-CONN-011) and Remove, which asks first
/// and leaves the row only after the API confirms. It holds no secret: the served view has none.
/// </summary>
internal sealed class ConnectionRowViewModel : ObservableObject
{
    private readonly ConnectionActions _actions;
    private readonly TimeProvider _clock;

    internal ConnectionRowViewModel(ConnectionView view, ConnectionActions actions, TimeProvider clock)
    {
        (View, _actions, _clock) = (view, actions, clock);
        Link = new DeepLink(DeepLinkKind.Connection, view.Name);
        Derive();
    }

    /// <summary>Raised once the API confirmed the connection is gone.</summary>
    internal event EventHandler? Removed;

    /// <summary>The connection as last served.</summary>
    internal ConnectionView View { get; private set; }

    /// <summary>The row's copy.</summary>
    public ConnectionsText Text { get; } = ConnectionsText.Instance;

    /// <summary>The deep link that opens its sheet.</summary>
    public DeepLink Link { get; }

    /// <summary>The connection's name.</summary>
    public string Name => View.Name;

    /// <summary>The kind in words.</summary>
    public string KindText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The status as a plain word (Connected); the tiles use the same.</summary>
    public string ChipText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The status as served ('connected', 'needs-sign-in', 'error').</summary>
    public string Status => View.Status;

    /// <summary>True while the connection is connected.</summary>
    public bool IsConnected => string.Equals(View.Status, "connected", StringComparison.Ordinal);

    /// <summary>True while the provider refused the credential and a sign-in is the fix.</summary>
    public bool NeedsSignIn => string.Equals(View.Status, "needs-sign-in", StringComparison.Ordinal);

    /// <summary>True while the last test failed for a reason other than a sign-in.</summary>
    public bool IsError => string.Equals(View.Status, "error", StringComparison.Ordinal);

    /// <summary>True while the connection needs a person: it is not connected.</summary>
    public bool IsProblem => !IsConnected;

    /// <summary>Why it is not connected, as served; empty otherwise.</summary>
    public string ReasonText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when there is a reason.</summary>
    public bool HasReason => ReasonText.Length > 0;

    /// <summary>"Refreshed 30m ago" for a connection whose token is refreshed in the background (oauth); empty for the others.</summary>
    public string RefreshedText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when there is a refresh line.</summary>
    public bool HasRefreshed => RefreshedText.Length > 0;

    /// <summary>The row's accessible name.</summary>
    public string AccessibleName { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the connection signs in through a browser, so Sign in again applies (oauth and mcp).</summary>
    public bool CanSignIn => string.Equals(View.Kind, "oauth", StringComparison.Ordinal) || string.Equals(View.Kind, "mcp", StringComparison.Ordinal);

    /// <summary>What the last action said: a test's outcome, the wait for the browser, a refusal; empty before any.</summary>
    public string NoteText
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasNote));
        }
    } = string.Empty;

    /// <summary>True when there is a note.</summary>
    public bool HasNote => NoteText.Length > 0;

    /// <summary>True while an action runs: the others wait.</summary>
    public bool IsBusy { get; private set => Set(ref field, value); }

    /// <summary>True while the row asks "Remove?" and waits for the answer.</summary>
    public bool IsConfirmingRemove { get; private set => Set(ref field, value); }

    /// <summary>True when the three actions show (not while they ask to remove).</summary>
    public bool ShowActions => !IsConfirmingRemove;

    /// <summary>The Test action's accessible name.</summary>
    public string TestName => Labels.ConnectionActionName(Labels.ConnectionTest, Name);

    /// <summary>The Sign in again action's accessible name.</summary>
    public string SignInName => Labels.ConnectionActionName(Labels.ConnectionSignIn, Name);

    /// <summary>The Remove action's accessible name.</summary>
    public string RemoveName => Labels.ConnectionActionName(Labels.ConnectionRemove, Name);

    /// <summary>The confirm's question.</summary>
    public string RemoveAsk => Labels.ConnectionRemoveAsk(Name);

    /// <summary>The confirm button's accessible name.</summary>
    public string RemoveConfirmName => Labels.ConnectionActionName(Labels.ConnectionRemoveConfirm, Name);

    /// <summary>Takes a newer serving of the same connection and redraws only what changed.</summary>
    /// <param name="view">The connection as served now.</param>
    internal void Update(ConnectionView view)
    {
        View = view;
        Derive();
    }

    /// <summary>Re-words the age (time passes without a read).</summary>
    internal void Age() => RefreshedText = Refreshed();

    /// <summary>Tests the connection and shows the outcome: ok, or the reason Sindri gives.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that ends when the outcome is shown.</returns>
    internal async Task Test(CancellationToken cancellationToken)
    {
        if (!Begin(Labels.ConnectionTesting))
        {
            return;
        }

        using var run = Run(cancellationToken);
        try
        {
            var tested = await _actions.Test(Name, run.Token).ConfigureAwait(true);
            if (tested.IsError)
            {
                NoteText = Labels.ConnectionWriteFailed(tested.Error);
                return;
            }

            Update(tested.Value.Connection);
            NoteText = tested.Value.Ok ? Labels.ConnectionTestOk(tested.Value.Tools?.Count) : Labels.ConnectionTestFailed(tested.Value.Reason);
        }
        catch (OperationCanceledException) when (run.IsClosed)
        {
            NoteText = string.Empty;   // the page or sheet closed: nothing is left to say
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Signs in again through the browser: asks Sindri, opens the consent page, then reads the connection until it is connected or the wait runs out. Not offered to a key connection.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that ends when the sign-in has ended.</returns>
    internal async Task SignIn(CancellationToken cancellationToken)
    {
        if (!CanSignIn || !Begin(string.Empty))
        {
            return;
        }

        using var run = Run(cancellationToken);
        try
        {
            var started = await _actions.SignIn(Name, run.Token).ConfigureAwait(true);
            if (started.IsError)
            {
                NoteText = Labels.ConnectionWriteFailed(started.Error);
                return;
            }

            Update(started.Value.Connection);
            var end = await _actions.Finish(started.Value, said => NoteText = said, run.Token).ConfigureAwait(true);
            if (end.Connection is { } last)
            {
                Update(last);
            }

#pragma warning disable IDE0072 // a failure is every other end
            NoteText = end.State switch
            {
                SignInState.Connected => Labels.ConnectionSignedIn,
                SignInState.TimedOut => Labels.ConnectionSignInTimedOut,
                _ => Labels.ConnectionSignInFailed(end.Detail),
            };
#pragma warning restore IDE0072
        }
        catch (OperationCanceledException) when (run.IsClosed)
        {
            NoteText = string.Empty;   // the page or sheet closed: the wait for the browser stops, and the next read shows where the sign-in got to
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Asks "Remove?" first; nothing is sent.</summary>
    internal void AskRemove()
    {
        if (!IsBusy)
        {
            SetConfirming(confirming: true);
        }
    }

    /// <summary>Backs out of the question.</summary>
    internal void Keep() => SetConfirming(confirming: false);

    /// <summary>Removes the connection. The row leaves only after the API confirms; a refusal keeps it and says why.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that ends when the answer is shown.</returns>
    internal async Task ConfirmRemove(CancellationToken cancellationToken)
    {
        if (!IsConfirmingRemove || !Begin(string.Empty))
        {
            return;
        }

        using var run = Run(cancellationToken);
        try
        {
            var removed = await _actions.Remove(Name, run.Token).ConfigureAwait(true);
            SetConfirming(confirming: false);
            if (removed.IsError)
            {
                NoteText = Labels.ConnectionWriteFailed(removed.Error);
                return;
            }
        }
        catch (OperationCanceledException) when (run.IsClosed)
        {
            SetConfirming(confirming: false);   // the page or sheet closed before the answer: the next read shows whether it is gone
            return;
        }
        finally
        {
            IsBusy = false;
        }

        Removed?.Invoke(this, EventArgs.Empty);
    }

    // One action's token: the caller's, and the owner's open token (ConnectionActions.Closing), so closing the page or the sheet stops the action.
    private ActionRun Run(CancellationToken cancellationToken) => new(_actions.Closing, cancellationToken);

    private bool Begin(string note)
    {
        if (IsBusy)
        {
            return false;
        }

        (IsBusy, NoteText) = (true, note);
        return true;
    }

    private void SetConfirming(bool confirming)
    {
        IsConfirmingRemove = confirming;
        OnChanged(nameof(ShowActions));
    }

    private void Derive()
    {
        var (_, word) = Labels.ConnectionChip(View.Status);
        (ChipText, KindText, ReasonText, RefreshedText) = (word, Labels.ConnectionKind(View.Kind), View.StatusReason ?? string.Empty, Refreshed());
        AccessibleName = Labels.ConnectionRowName(View.Name, KindText, word.ToLowerInvariant(), ReasonText);
        foreach (var name in (string[])[nameof(Status), nameof(IsConnected), nameof(NeedsSignIn), nameof(IsError), nameof(IsProblem), nameof(HasReason), nameof(HasRefreshed), nameof(CanSignIn)])
        {
            OnChanged(name);
        }
    }

    private string Refreshed() =>
        string.Equals(View.Kind, "oauth", StringComparison.Ordinal) ? Labels.ConnectionRefreshed(View.LastRefreshedAt is { } at ? _clock.GetUtcNow() - at : null) : string.Empty;

    /// <summary>An action's token, linked to the owner's open token; <see cref="IsClosed"/> tells a close from the caller's own cancel.</summary>
    private sealed class ActionRun(CancellationToken closing, CancellationToken caller) : IDisposable
    {
        private readonly CancellationTokenSource _linked = CancellationTokenSource.CreateLinkedTokenSource(closing, caller);

        internal CancellationToken Token => _linked.Token;

        private readonly CancellationToken _closing = closing;

        internal bool IsClosed => _closing.IsCancellationRequested;

        public void Dispose() => _linked.Dispose();
    }
}
