using Aw.Sindri.Ui.Api.Connections;

namespace Aw.Sindri.Ui.ViewModels.Connections;

/// <summary>
/// The Add connection form (PR 18a). A preset picker chooses what to connect: an OAuth provider (the browser opens the consent page and the form waits until the connection reads
/// 'connected'), a service with a key (rest or cli), or an MCP server (its address). A pasted key or client secret goes into ONE request and is then gone: it is taken out of its
/// <see cref="SecretField"/> to build the body, never held in a property, a note or an error after that, and never written to the app's preferences.
/// </summary>
#pragma warning disable CA1001 // why: the source has no timer and holds no resource; Close disposes it and makes the next
internal sealed class AddConnectionViewModel : ObservableObject
{
    private static readonly ConnectionPresetChoice[] _presets = [.. Labels.ConnectionPresets.Select(p => new ConnectionPresetChoice(p.Id, p.Label, p.Kind))];

    private readonly ConnectionActions _actions;
    private readonly Func<Task> _changed;
    private CancellationTokenSource _run = new();

    /// <summary>The form over its calls.</summary>
    /// <param name="actions">The calls.</param>
    /// <param name="changed">Reads the list again after a write, so the new row is there.</param>
    internal AddConnectionViewModel(ConnectionActions actions, Func<Task> changed)
    {
        (_actions, _changed) = (actions, changed);
        Key.PropertyChanged += (_, _) => OnChanged(nameof(CanSend));
    }

    /// <summary>The form's copy.</summary>
    public ConnectionsText Text { get; } = ConnectionsText.Instance;

    /// <summary>What can be connected.</summary>
#pragma warning disable CA1822, MA0041 // a fixed list is a property because the view binds it
    public IReadOnlyList<ConnectionPresetChoice> Presets => _presets;
#pragma warning restore CA1822, MA0041

    /// <summary>The pasted key (rest and cli).</summary>
    public SecretField Key { get; } = new();

    /// <summary>The pasted OAuth client secret (only for a provider that asks for one).</summary>
    public SecretField ClientSecret { get; } = new();

    /// <summary>True while the form is shown.</summary>
    public bool IsOpen { get; private set => Set(ref field, value); }

    /// <summary>The choice in the picker. Choosing another drops anything pasted for the last.</summary>
    public ConnectionPresetChoice SelectedPreset
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            DropSecrets();
            field = value;
            foreach (var name in (string[])[nameof(SelectedPreset), nameof(ShowClient), nameof(ShowClientSecret), nameof(ShowKey), nameof(ShowUrl), nameof(ShowSecretNote), nameof(CanSend)])
            {
                OnChanged(name);
            }
        }
    } = _presets[0];

    /// <summary>The connection's name ('linear:work').</summary>
    public string Name
    {
        get;
        set
        {
            field = value ?? string.Empty;
            OnChanged(nameof(Name));
            OnChanged(nameof(CanSend));
        }
    } = string.Empty;

    /// <summary>The OAuth client id (not a secret).</summary>
    public string ClientId
    {
        get;
        set
        {
            field = value ?? string.Empty;
            OnChanged(nameof(ClientId));
            OnChanged(nameof(CanSend));
        }
    } = string.Empty;

    /// <summary>The MCP server's address.</summary>
    public string Url
    {
        get;
        set
        {
            field = value ?? string.Empty;
            OnChanged(nameof(Url));
            OnChanged(nameof(CanSend));
        }
    } = string.Empty;

    /// <summary>True when the client id and secret fields show (OAuth).</summary>
    public bool ShowClient => SelectedPreset.IsOAuth;

    /// <summary>True when the client secret field shows (OAuth).</summary>
    public bool ShowClientSecret => SelectedPreset.IsOAuth;

    /// <summary>True when the key field shows (rest and cli).</summary>
    public bool ShowKey => string.Equals(SelectedPreset.Kind, "rest", StringComparison.Ordinal) || string.Equals(SelectedPreset.Kind, "cli", StringComparison.Ordinal);

    /// <summary>True when the server address field shows (mcp).</summary>
    public bool ShowUrl => string.Equals(SelectedPreset.Kind, "mcp", StringComparison.Ordinal);

    /// <summary>True when the note about where a pasted secret goes shows (a key or a client secret is asked for).</summary>
    public bool ShowSecretNote => ShowKey || ShowClientSecret;

    /// <summary>True while a send runs.</summary>
    public bool IsWorking { get; private set => Set(ref field, value); }

    /// <summary>True when the form has what its kind needs and nothing is being sent.</summary>
    public bool CanSend => !IsWorking && Name.Trim().Length > 0 && Needed();

    /// <summary>What the form says it is doing: sending, waiting for the browser, done; empty otherwise.</summary>
    public string StatusText
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasStatus));
        }
    } = string.Empty;

    /// <summary>True when there is a status.</summary>
    public bool HasStatus => StatusText.Length > 0;

    /// <summary>Why the last send didn't work, in words with Sindri's code; empty otherwise.</summary>
    public string ErrorText
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasError));
        }
    } = string.Empty;

    /// <summary>True when there is an error.</summary>
    public bool HasError => ErrorText.Length > 0;

    /// <summary>Shows the form.</summary>
    internal void Open() => IsOpen = true;

    /// <summary>Closes the form and drops anything pasted.</summary>
    internal void Close()
    {
        DropSecrets();
        IsOpen = false;
        _run.Cancel();   // a wait for the browser stops here, and what it would still say is dropped
        _run.Dispose();
        _run = new CancellationTokenSource();
        (IsWorking, StatusText, ErrorText) = (false, string.Empty, string.Empty);
        OnChanged(nameof(CanSend));
    }

    /// <summary>
    /// Sends the form: one POST, with any pasted secret taken out of its field first. An OAuth connection then opens the consent page and waits until it reads 'connected' or the wait runs out.
    /// A refusal keeps the name and the other fields, says why, and leaves the secret gone. Closing the form cancels the wait.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A task that ends when the form has its answer.</returns>
    internal async Task Send(CancellationToken cancellationToken)
    {
        if (!CanSend)
        {
            return;
        }

        var name = Name.Trim();
        var run = _run;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, run.Token);
        (IsWorking, ErrorText, StatusText) = (true, string.Empty, Labels.ConnectionAdding);
        OnChanged(nameof(CanSend));
        try
        {
            // the body, with any pasted secret, lives only as this argument: after the POST the form holds the name and nothing else
            var added = await _actions.Add(Body(), linked.Token).ConfigureAwait(true);
            if (added.IsError)
            {
                (ErrorText, StatusText) = (Labels.ConnectionWriteFailed(added.Error), string.Empty);
                return;
            }

            await Follow(added.Value, name, run, linked.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (run.IsCancellationRequested)
        {
            // the form was closed while it waited: Close already cleared it
        }
        finally
        {
            if (!run.IsCancellationRequested)
            {
                IsWorking = false;
                OnChanged(nameof(CanSend));
            }
        }
    }

    // After the POST: a key connection's row exists already, an OAuth one only once the callback lands. Nothing is written once the form was closed.
    private async Task Follow(ConnectionAdded added, string name, CancellationTokenSource run, CancellationToken cancellationToken)
    {
        if (added.ConsentUrl is null)
        {
            await _changed().ConfigureAwait(true);
        }

        var end = await _actions.Finish(added, said => Say(run, said), cancellationToken).ConfigureAwait(true);
        await _changed().ConfigureAwait(true);
        if (!run.IsCancellationRequested)
        {
            Ended(name, end);
        }
    }

    private void Say(CancellationTokenSource run, string text)
    {
        if (!run.IsCancellationRequested)
        {
            StatusText = text;
        }
    }

    private void DropSecrets()
    {
        _ = Key.Take();
        _ = ClientSecret.Take();
    }

    private void Ended(string name, SignInEnd end)
    {
        if (end.State == SignInState.Connected)
        {
            (StatusText, Name, ClientId, Url) = (Labels.ConnectionAdded(name), string.Empty, string.Empty, string.Empty);
            return;
        }

        (StatusText, ErrorText) = (string.Empty, end.State == SignInState.TimedOut ? Labels.ConnectionSignInTimedOut : Labels.ConnectionSignInFailed(end.Detail));
    }

    // The body for the one request. A secret is taken out of its field here and lives only in this body.
    private AddConnectionBody Body()
    {
        var choice = SelectedPreset;
        var name = Name.Trim();
        if (choice.IsOAuth)
        {
            return new AddConnectionBody(name, choice.Id, Kind: null, OrEmpty(ClientId), KeyEnv: null, Scopes: [], Wait: false, ClientSecret: ClientSecret.Take());
        }

        return choice.Kind switch
        {
            "mcp" => new AddConnectionBody(name, Preset: null, choice.Kind, Client: null, KeyEnv: null, Scopes: [], Wait: false, Url: OrEmpty(Url)),
            _ => new AddConnectionBody(name, Preset: null, choice.Kind, Client: null, KeyEnv: null, Scopes: [], Wait: false, Key: Key.Take()),
        };
    }

    private bool Needed() => SelectedPreset.Kind switch
    {
        "oauth" => ClientId.Trim().Length > 0,   // a preset ships no client id (SND-CONN-007)
        "mcp" => Url.Trim().Length > 0,
        _ => Key.HasValue,
    };

    private static string? OrEmpty(string text) => text.Trim() is { Length: > 0 } trimmed ? trimmed : null;
}
#pragma warning restore CA1001
