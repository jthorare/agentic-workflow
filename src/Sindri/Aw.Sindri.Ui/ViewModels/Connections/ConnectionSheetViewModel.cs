using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Connections;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Connections;

/// <summary>
/// The connection sheet (the GET route, connection_get): one connection with its status, its settings (never a secret: only whether one is stored) and the same actions as its row.
/// It reads the connection on open and again on every event while the sheet is open (the shell gates that).
/// </summary>
internal sealed class ConnectionSheetViewModel : ObservableObject
{
    private readonly TimeProvider _clock;
    private readonly ConnectionActions _actions;
    private Uri? _url;
    private int _generation;

    /// <summary>The sheet over its ports.</summary>
    /// <param name="api">The API.</param>
    /// <param name="launcher">Opens a consent page.</param>
    /// <param name="clock">The clock the ages and the sign-in poll run on.</param>
    internal ConnectionSheetViewModel(ISindriApi api, ILauncher launcher, TimeProvider clock)
    {
        _clock = clock;
        _actions = new ConnectionActions(api, () => _url, launcher, clock);
    }

    /// <summary>Raised once the API confirmed the connection is gone; the shell closes the sheet.</summary>
    internal event EventHandler? Removed;

    /// <summary>The sheet's copy.</summary>
    public ConnectionsText Text { get; } = ConnectionsText.Instance;

    /// <summary>Where the read stands.</summary>
    public FramePhase Phase
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(IsShown));
            OnChanged(nameof(IsLoading));
            OnChanged(nameof(IsProblem));
        }
    }

    /// <summary>True while a connection is shown.</summary>
    public bool IsShown => Phase == FramePhase.Shown;

    /// <summary>True before the first answer.</summary>
    public bool IsLoading => Phase == FramePhase.Loading;

    /// <summary>True while an unreadable, unknown or refused answer is the whole sheet.</summary>
    public bool IsProblem => Phase is FramePhase.Unreadable or FramePhase.Refused or FramePhase.NotFound;

    /// <summary>The connection's name; empty before the first.</summary>
    public string Heading { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The connection, with its actions; null until it is read.</summary>
    public ConnectionRowViewModel? Row { get; private set => Set(ref field, value); }

    /// <summary>The preset and the kind, one line.</summary>
    public string KindLine { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The non-secret settings Sindri keeps, as "name: value" lines in name order.</summary>
    public IReadOnlyList<string> SettingLines
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasSettings));
        }
    } = [];

    /// <summary>True when there are settings to list.</summary>
    public bool HasSettings => SettingLines.Count > 0;

    /// <summary>Whether a secret is stored, in words.</summary>
    public string SecretLine { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>A refused or unreadable answer's sentence; empty otherwise.</summary>
    public string ProblemText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The stale note: the last read failed and the fields are the last read's.</summary>
    public string Note { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Names the connection now, before any read (a deep link, even before the app is connected).</summary>
    /// <param name="name">The connection's name.</param>
    internal void Show(string name)
    {
        if (string.Equals(name, Heading, StringComparison.Ordinal))
        {
            return;
        }

        _generation++;
        if (Row is { } old)
        {
            old.Removed -= OnRemoved;
            _actions.Close();   // the old connection's action still running stops with it
        }

        (Row, KindLine, SecretLine, ProblemText, Note, SettingLines) = (null, string.Empty, string.Empty, string.Empty, string.Empty, []);
        Heading = name;
        Phase = FramePhase.Loading;
    }

    /// <summary>Reads the connection behind a name and shows it.</summary>
    /// <param name="url">The base URL.</param>
    /// <param name="name">The connection's name.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that ends when the read has been drawn.</returns>
    internal Task Open(Uri url, string name, CancellationToken cancellationToken)
    {
        Show(name);
        _url = url;
        return Read(cancellationToken);
    }

    /// <summary>Reads the open connection again (on every event while the sheet is open).</summary>
    /// <param name="url">The base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that ends when the read has been drawn.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        _url = url;
        return Heading.Length == 0 ? Task.CompletedTask : Read(cancellationToken);
    }

    /// <summary>The sheet was closed: a row action still waiting (a sign-in's browser wait) stops.</summary>
    internal void Close() => _actions.Close();

    private async Task Read(CancellationToken cancellationToken)
    {
        if (Heading.Length == 0)
        {
            return;
        }

        var generation = ++_generation;
        var answer = await _actions.Get(Heading, cancellationToken).ConfigureAwait(true);
        if (generation != _generation)
        {
            return;   // a newer read, or another connection, took over
        }

        if (answer.IsSuccess)
        {
            Draw(answer.Value.Connection);
        }
        else
        {
            Failed(answer.Error);
        }
    }

    private void Draw(ConnectionView view)
    {
        if (Row is null)
        {
            Row = new ConnectionRowViewModel(view, _actions, _clock);
            Row.Removed += OnRemoved;
        }
        else
        {
            Row.Update(view);
        }

        KindLine = string.Join(" · ", new[] { Labels.ConnectionKind(view.Kind), view.Preset ?? string.Empty }.Where(p => p.Length > 0));
        SettingLines = [.. view.Config.OrderBy(c => c.Key, StringComparer.Ordinal).Select(c => c.Key + ": " + c.Value)];
        SecretLine = Labels.ConnectionSecretLine(view.HasSecret);
        (ProblemText, Note) = (string.Empty, string.Empty);
        Phase = FramePhase.Shown;
    }

    private void Failed(Error error)
    {
        if (IsShown)
        {
            Note = Labels.ConnectionStale(error);
            return;
        }

        var unreadable = string.Equals(error.Code, SindriUiErrors.BadAnswer.Code, StringComparison.Ordinal);
        var unknown = !unreadable && error.Kind is ErrorKind.NotFound or ErrorKind.Invalid;
        ProblemText = unknown ? Labels.ConnectionUnknown : unreadable ? Labels.ConnectionUnreadable : Labels.InstanceRefused(error);
        Phase = unknown ? FramePhase.NotFound : unreadable ? FramePhase.Unreadable : FramePhase.Refused;
    }

    private void OnRemoved(object? sender, EventArgs e) => Removed?.Invoke(this, EventArgs.Empty);
}
