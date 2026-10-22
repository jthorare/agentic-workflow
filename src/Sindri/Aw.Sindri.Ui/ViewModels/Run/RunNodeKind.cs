namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>What a card on the run canvas is (F3 spec 3 §2): a stage of the main path, a side card for a branch the run can loop through, or an end.</summary>
internal enum RunNodeKind
{
    /// <summary>A stage on the main path: 320 px wide, its steps listed inside.</summary>
    Stage,

    /// <summary>A branch card (a redesign): 280 px wide, no step strip.</summary>
    Side,

    /// <summary>An end of the run (merged, stuck, stopped): 280 px wide, no step strip.</summary>
    End,
}
