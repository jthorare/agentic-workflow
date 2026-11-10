using Aw.Sindri.Ui.Api.Connections;

namespace Aw.Sindri.Ui.ViewModels.Connections;

/// <summary>
/// One service tile on the Connections page (#2537): a service with a real sign-in path. Connect runs it and names the connection itself ('github:work'). A service whose
/// provider needs a client id asks for it on the tile; Slack has no browser sign-in, so its tile takes a pasted token. Once connected the tile shows the account, last use,
/// Sign in again and a menu with Disconnect. A failed or cancelled sign-in shows on the tile with Try again. A pasted token goes into one request and is gone.
/// </summary>
internal sealed class ServiceTileViewModel : ObservableObject
{
    private const string DefaultLabel = "work";

    private readonly ConnectionActions _actions;
    private readonly Func<Task> _changed;
    private readonly TimeProvider _clock;
    private ConnectionRowViewModel? _row;

    internal ServiceTileViewModel(ServiceTile service, ConnectionActions actions, Func<Task> changed, TimeProvider clock)
    {
        (Service, _actions, _changed, _clock) = (service, actions, changed, clock);
        Token.PropertyChanged += (_, _) => Raise(nameof(CanSend), nameof(BlockedReason));
    }

    /// <summary>The service this tile is for.</summary>
    internal ServiceTile Service { get; }

    /// <summary>The tile's copy.</summary>
    public ConnectionsText Text { get; } = ConnectionsText.Instance;

    /// <summary>The service id ('github').</summary>
    public string Id => Service.Id;

    /// <summary>The service in words ('GitHub').</summary>
    public string Title => Service.Title;

    /// <summary>The line under the title: what Connect does, or the account once connected.</summary>
    public string Subtitle => IsConnected ? Account : Service.Blurb;

    /// <summary>The pasted token (Slack).</summary>
    public SecretField Token { get; } = new();

    /// <summary>The OAuth client id typed on the tile.</summary>
    public string ClientId
    {
        get;
        set
        {
            field = value ?? string.Empty;
            Raise(nameof(ClientId), nameof(CanSend), nameof(BlockedReason));
        }
    } = string.Empty;

    /// <summary>True when the sign-in needs a client id.</summary>
    public bool NeedsClientId => Service.Mode == TileMode.ClientId;

    /// <summary>True when the sign-in takes a pasted token.</summary>
    public bool TakesToken => Service.Mode == TileMode.Token;

    /// <summary>True while the tile has no connection.</summary>
    public bool IsOffered => _row is null;

    /// <summary>True while the tile asks for its input before it connects.</summary>
    public bool IsAsking { get; private set => Set(ref field, value); }

    /// <summary>True while a sign-in runs.</summary>
    public bool IsWorking { get; private set => Set(ref field, value); }

    /// <summary>True when the connection reads 'connected'.</summary>
    public bool IsConnected => _row is { IsConnected: true };

    /// <summary>True when the connection exists but needs a sign-in or reads an error.</summary>
    public bool NeedsAttention => _row is { IsConnected: false };

    /// <summary>True when Connect shows (no connection, nothing running).</summary>
    public bool ShowConnect => IsOffered && !IsWorking && !HasError;

    /// <summary>The connection's name ('github:work'), or empty.</summary>
    public string ConnectionName => _row?.Name ?? string.Empty;

    /// <summary>The account the tile signed in as: the label of the connection name ('work').</summary>
    public string Account
    {
        get
        {
            var name = ConnectionName;
            var at = name.IndexOf(':', StringComparison.Ordinal);
            return at >= 0 ? name[(at + 1)..] : name;
        }
    }

    /// <summary>"Last used 5m ago", or "Not used yet".</summary>
    public string LastUsedText => _row is { View.LastRefreshedAt: { } at } ? Labels.ConnectionLastUsed(_clock.GetUtcNow() - at) : Labels.ConnectionNeverUsed;

    /// <summary>Why Connect can't send yet, in words; empty when it can.</summary>
    public string BlockedReason => NeedsClientId && ClientId.Trim().Length == 0 ? Labels.ConnectNeedsClientId(Service.Title)
        : TakesToken && !Token.HasValue ? Labels.ConnectNeedsToken(Service.Title) : string.Empty;

    /// <summary>True when the inline line explains a disabled send.</summary>
    public bool HasBlockedReason => IsAsking && BlockedReason.Length > 0;

    /// <summary>True when the input is complete and nothing runs.</summary>
    public bool CanSend => !IsWorking && BlockedReason.Length == 0;

    /// <summary>What the tile says while it works (the wait for the browser).</summary>
    public string StatusText { get; private set { Set(ref field, value); OnChanged(nameof(HasStatus)); } } = string.Empty;

    /// <summary>True when there is a status.</summary>
    public bool HasStatus => StatusText.Length > 0;

    /// <summary>Why the last try failed, in words; empty otherwise.</summary>
    public string ErrorText { get; private set { Set(ref field, value); Raise(nameof(HasError), nameof(ShowConnect)); } } = string.Empty;

    /// <summary>True when the last try failed or was cancelled.</summary>
    public bool HasError => ErrorText.Length > 0;

    /// <summary>The label on the Connect button: "Connect", or "Add token" for Slack.</summary>
    public string ConnectText => TakesToken ? Labels.ConnectAddToken : Labels.ConnectTile;

    /// <summary>The tile's accessible name.</summary>
    public string AccessibleName => IsConnected ? Title + ", connected as " + Account : Title + ", " + (HasError ? ErrorText : Service.Blurb);

    /// <summary>The connection this tile shows, or null: the page sets it on every read.</summary>
    /// <param name="row">The matching row.</param>
    internal void Bind(ConnectionRowViewModel? row)
    {
        _row = row;
        Raise(nameof(IsOffered), nameof(IsConnected), nameof(NeedsAttention), nameof(ShowConnect), nameof(ConnectionName), nameof(Account), nameof(LastUsedText), nameof(Subtitle), nameof(AccessibleName));
    }

    /// <summary>Connect or Try again was pressed: asks for the input first when the service needs one, then signs in.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that ends when the tile has its answer.</returns>
    internal async Task Connect(CancellationToken cancellationToken)
    {
        if (IsWorking)
        {
            return;
        }

        ErrorText = string.Empty;
        if ((NeedsClientId || TakesToken) && !IsAsking)
        {
            IsAsking = true;
            OnChanged(nameof(HasBlockedReason));
            return;
        }

        if (!CanSend)
        {
            return;
        }

        IsWorking = true;
        Raise(nameof(CanSend), nameof(ShowConnect));
        StatusText = Labels.ConnectionAdding;
        try
        {
            await Run(cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            ErrorText = Labels.ConnectCancelled;
        }
        finally
        {
            (IsWorking, StatusText) = (false, string.Empty);
            Raise(nameof(CanSend), nameof(ShowConnect), nameof(HasBlockedReason));
        }
    }

    // Adds the connection, then follows the sign-in to its end; whatever went wrong shows on the tile.
    private async Task Run(CancellationToken cancellationToken)
    {
        var added = await _actions.Add(Body(), cancellationToken).ConfigureAwait(true);
        if (added.IsError)
        {
            ErrorText = Labels.ConnectTileFailed(added.Error);
            return;
        }

        if (added.Value.ConsentUrl is null)
        {
            await _changed().ConfigureAwait(true);
        }

        var end = await _actions.Finish(added.Value, said => StatusText = said, cancellationToken).ConfigureAwait(true);
        await _changed().ConfigureAwait(true);
        if (end.State == SignInState.Connected)
        {
            (IsAsking, ClientId) = (false, string.Empty);
            return;
        }

        ErrorText = end.State == SignInState.TimedOut ? Labels.ConnectionSignInTimedOut : Labels.ConnectionSignInFailed(end.Detail);
    }

    /// <summary>Backs out of the input.</summary>
    internal void Cancel()
    {
        _ = Token.Take();
        (IsAsking, ErrorText) = (false, string.Empty);
        OnChanged(nameof(HasBlockedReason));
    }

    /// <summary>Signs in again on the connected tile.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that ends with the sign-in.</returns>
    internal Task SignInAgain(CancellationToken cancellationToken) => _row?.SignIn(cancellationToken) ?? Task.CompletedTask;

    /// <summary>Disconnects: removes the connection and its stored secret.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that ends when the answer is shown.</returns>
    internal async Task Disconnect(CancellationToken cancellationToken)
    {
        if (_row is not { } row)
        {
            return;
        }

        row.AskRemove();
        await row.ConfirmRemove(cancellationToken).ConfigureAwait(true);
        if (row.NoteText.Length > 0)
        {
            ErrorText = row.NoteText;
        }
    }

    private AddConnectionBody Body()
    {
        var name = Service.Id + ":" + DefaultLabel;
        return Service.Mode == TileMode.Token
            ? new AddConnectionBody(name, Preset: null, Kind: "rest", Client: null, KeyEnv: null, Scopes: [], Wait: false, Key: Token.Take())
            : new AddConnectionBody(name, Service.Id, Kind: null, ClientId.Trim(), KeyEnv: null, Scopes: [], Wait: false);
    }

    private void Raise(params string[] names)
    {
        foreach (var n in names)
        {
            OnChanged(n);
        }
    }
}
