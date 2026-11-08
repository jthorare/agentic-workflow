namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>What an attention row of the Todo panel is (todo spec §3.1): a both-places conflict on one todo, or the hold on a mass change from the note.</summary>
internal enum AttentionKind
{
    /// <summary>A todo changed in Sindri and in Obsidian (D11).</summary>
    Conflict,

    /// <summary>The note changed many todos at once and Sindri holds them (REQ-E7).</summary>
    Hold,
}
