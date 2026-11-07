namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>How one field of the Linear issue sheet is edited.</summary>
internal enum LinearEditKind
{
    /// <summary>A text box (the title, the description).</summary>
    Text,

    /// <summary>A picker of one choice (state, priority, assignee, cycle).</summary>
    Choice,

    /// <summary>Ticks, any number of them (labels).</summary>
    Ticks,

    /// <summary>Shown, never edited (project).</summary>
    Fixed,
}
