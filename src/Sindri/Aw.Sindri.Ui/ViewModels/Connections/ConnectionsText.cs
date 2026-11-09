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

    /// <summary><see cref="Labels.ConnectionNameHint"/>.</summary>
    public string NameHint { get; } = Labels.ConnectionNameHint;

    /// <summary><see cref="Labels.ConnectionClientLabel"/>.</summary>
    public string ClientLabel { get; } = Labels.ConnectionClientLabel;

    /// <summary><see cref="Labels.ConnectionClientSecretLabel"/>.</summary>
    public string ClientSecretLabel { get; } = Labels.ConnectionClientSecretLabel;

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

    /// <summary><see cref="Labels.ConnectionSlackNote"/>.</summary>
    public string SlackNote { get; } = Labels.ConnectionSlackNote;

    /// <summary><see cref="Labels.ReadingConnection"/>.</summary>
    public string ReadingOne { get; } = Labels.ReadingConnection;

    /// <summary><see cref="Labels.ConnectionSettings"/>.</summary>
    public string Settings { get; } = Labels.ConnectionSettings;
}
