#pragma warning disable CA1822, MA0041, MA0182 // red commit: stubs, replaced by the green commit
namespace Aw.Sindri.Ui.ViewModels.Connections;

/// <summary>The Connections page's own copy (<see cref="Labels"/>) as its views bind it: compiled bindings resolve public members only, and the labels stay internal.</summary>
internal sealed class ConnectionsText
{
    /// <summary>The one instance.</summary>
    internal static ConnectionsText Instance { get; } = new();

    /// <summary><see cref="Labels.ConnectionsPage"/>.</summary>
    public string Title { get; } = Labels.ConnectionsPage;

    /// <summary><see cref="Labels.ConnectionsPurpose"/>.</summary>
    public string Purpose { get; } = Labels.ConnectionsPurpose;

    /// <summary><see cref="Labels.ReadingConnections"/>.</summary>
    public string Reading { get; } = Labels.ReadingConnections;

    /// <summary><see cref="Labels.ConnectionsUnreadable"/>.</summary>
    public string Unreadable { get; } = Labels.ConnectionsUnreadable;

    /// <summary><see cref="Labels.TryAgain"/>.</summary>
    public string TryAgain { get; } = Labels.TryAgain;

    /// <summary><see cref="Labels.Stale"/>.</summary>
    public string Stale { get; } = Labels.Stale;

    /// <summary><see cref="Labels.ConnectionTest"/>.</summary>
    public string Test { get; } = Labels.ConnectionTest;

    /// <summary><see cref="Labels.ConnectionSignIn"/>.</summary>
    public string SignIn { get; } = Labels.ConnectionSignIn;

    /// <summary><see cref="Labels.ConnectionRemove"/>.</summary>
    public string Remove { get; } = Labels.ConnectionRemove;

    /// <summary><see cref="Labels.ConnectionRemoveConfirm"/>.</summary>
    public string RemoveConfirm { get; } = Labels.ConnectionRemoveConfirm;

    /// <summary><see cref="Labels.ConnectionKeep"/>.</summary>
    public string Keep { get; } = Labels.ConnectionKeep;

    /// <summary><see cref="Labels.AddConnection"/>.</summary>
    public string Add { get; } = Labels.AddConnection;

    /// <summary><see cref="Labels.ConnectionPresetLabel"/>.</summary>
    public string PresetLabel { get; } = Labels.ConnectionPresetLabel;

    /// <summary><see cref="Labels.ConnectionNameLabel"/>.</summary>
    public string NameLabel { get; } = Labels.ConnectionNameLabel;

    /// <summary><see cref="Labels.ConnectionKeyLabel"/>.</summary>
    public string KeyLabel { get; } = Labels.ConnectionKeyLabel;

    /// <summary><see cref="Labels.ConnectionUrlLabel"/>.</summary>
    public string UrlLabel { get; } = Labels.ConnectionUrlLabel;

    /// <summary><see cref="Labels.ConnectionSecretNote"/>.</summary>
    public string SecretNote { get; } = Labels.ConnectionSecretNote;

    /// <summary><see cref="Labels.ConnectionAddSend"/>.</summary>
    public string Send { get; } = Labels.ConnectionAddSend;

    /// <summary><see cref="Labels.ConnectionAddCancel"/>.</summary>
    public string Cancel { get; } = Labels.ConnectionAddCancel;

    /// <summary><see cref="Labels.ReadingConnection"/>.</summary>
    public string ReadingOne { get; } = Labels.ReadingConnection;

    /// <summary><see cref="Labels.ConnectionSettings"/>.</summary>
    public string Settings { get; } = Labels.ConnectionSettings;

    /// <summary><see cref="Labels.ConnectionOthers"/>.</summary>
    public string Others { get; } = Labels.ConnectionOthers;

    /// <summary><see cref="Labels.ConnectionDisconnect"/>.</summary>
    public string Disconnect { get; } = Labels.ConnectionDisconnect;

    /// <summary><see cref="Labels.ConnectionMore"/>.</summary>
    public string More { get; } = Labels.ConnectionMore;

    /// <summary>The label of a tile's client id field.</summary>
    public string TileClientLabel { get; } = "Client id";

    /// <summary>The label of a tile's client secret field.</summary>
    public string TileSecretLabel { get; } = "Client secret";

    /// <summary><see cref="Labels.ConnectionKeyLabel"/> for a tile.</summary>
    public string TileTokenLabel { get; } = "Token";

    /// <summary><see cref="Labels.TileRename"/>.</summary>
    public string Rename { get; } = Labels.TileRename;

    /// <summary><see cref="Labels.TileRenameLabel"/>.</summary>
    public string RenameLabel { get; } = Labels.TileRenameLabel;

    /// <summary><see cref="Labels.TileRenameSave"/>.</summary>
    public string RenameSave { get; } = Labels.TileRenameSave;

    /// <summary><see cref="Labels.TileReconnect"/>.</summary>
    public string Reconnect { get; } = Labels.TileReconnect;
}
