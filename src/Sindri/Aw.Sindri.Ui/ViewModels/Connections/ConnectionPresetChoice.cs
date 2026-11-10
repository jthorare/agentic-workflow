namespace Aw.Sindri.Ui.ViewModels.Connections;

/// <summary>One row of the Add connection form's picker: what to connect and how it signs in.</summary>
/// <param name="Id">The kind's own word ('rest', 'cli', 'mcp').</param>
/// <param name="Label">The words in the picker.</param>
/// <param name="Kind">The kind's wire name.</param>
internal sealed record ConnectionPresetChoice(string Id, string Label, string Kind)
{
    /// <summary>The picker shows the label.</summary>
    /// <returns>The label.</returns>
    public override string ToString() => Label;
}
