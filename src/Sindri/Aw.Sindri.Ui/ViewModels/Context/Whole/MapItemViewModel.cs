namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>One entry of the map's accessible list: a feature (a tile of the overview) or a node, by the name the map draws.</summary>
/// <param name="Key">The node's or feature's key.</param>
/// <param name="Label">The words the map shows for it.</param>
/// <param name="Name">The accessible name: the label and what it is.</param>
/// <param name="IsTile">True for a feature of the overview, which Enter opens; false for a node, which Enter selects on the map.</param>
internal sealed record MapItemViewModel(string Key, string Label, string Name, bool IsTile)
{
    /// <summary>The file's full path ("src/ledger/Events.cs"); empty for a node that is not a file (#3677).</summary>
    internal string Path { get; init; } = string.Empty;

    /// <summary>The directory part of the path with its slash, drawn dimmed: "src/ledger/"; not built yet.</summary>
    public string Directory => IsTile && Path.Length == int.MaxValue ? Path : string.Empty;

    /// <summary>The file name, or the label when the node is not a file; not built yet.</summary>
    public string FileName => Label;
}
