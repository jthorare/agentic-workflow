namespace Aw.Sindri.Ui.ViewModels.Connections;

/// <summary>The accessible names and automation ids of a connection's actions (<see cref="ConnectionRowViewModel"/>, continued).</summary>
internal sealed partial class ConnectionRowViewModel
{
    /// <summary>The Test action's accessible name.</summary>
    public string TestName => Labels.ConnectionActionName(Labels.ConnectionTest, Name);

    /// <summary>The line's Rename button's accessible name: "Rename linear:work", so two lines don't both read "Rename".</summary>
    public string RenameName => Labels.ConnectionActionName(Labels.TileRename, Name);

    /// <summary>The line's Rename button's automation id: "ServiceTileRename.linear:work".</summary>
    public string RenameId => "ServiceTileRename." + Name;

    /// <summary>The Sign in again action's accessible name.</summary>
    public string SignInName => Labels.ConnectionActionName(Labels.ConnectionSignIn, Name);

    /// <summary>The Remove action's accessible name.</summary>
    public string RemoveName => Labels.ConnectionActionName(Labels.ConnectionRemove, Name);

    /// <summary>The confirm's question.</summary>
    public string RemoveAsk => Labels.ConnectionRemoveAsk(Name);

    /// <summary>The confirm button's accessible name.</summary>
    public string RemoveConfirmName => Labels.ConnectionActionName(Labels.ConnectionRemoveConfirm, Name);
}
