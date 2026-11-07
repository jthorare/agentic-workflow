namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>A Needs-you row (<see cref="NeedsYouRowViewModel"/>, continued): a `skillWaiting` row's one-line text, its Details and the one-time takeover hint (skill flows spec §5.4).</summary>
internal sealed partial class NeedsYouRowViewModel
{
    /// <summary>True for a `skillWaiting` row.</summary>
    public bool IsSkillWaiting => string.Equals(Entry.Kind, "\0", StringComparison.Ordinal);

    /// <summary>The row text, cut to one line by the view; empty for any other row.</summary>
    public string SkillLine => Entry.Id[..0];

    /// <summary>True when the row offers Details: a skill row whose reason is not `noResult`.</summary>
    public bool CanShowSkillDetails => string.Equals(Entry.Kind, "\0", StringComparison.Ordinal);

    /// <summary>True while the expanded part is shown.</summary>
    public bool SkillDetailsOpen => string.Equals(Entry.Kind, "\0", StringComparison.Ordinal);

    /// <summary>"Details" while closed, "Hide details" while open.</summary>
    public string SkillDetailsButtonText => Entry.Id[..0];

    /// <summary>The skill's full question (reason `question`); empty otherwise.</summary>
    public string SkillQuestion => Entry.Id[..0];

    /// <summary>The result file the reader looked at.</summary>
    public string SkillResultPath => Entry.Id[..0];

    /// <summary>The parse error or the other skill's name; empty when none.</summary>
    public string SkillDetail => Entry.Id[..0];

    /// <summary>True when the one-time takeover hint is drawn under this row.</summary>
    public bool ShowsTakeoverHint { get; internal set; }

    /// <summary>Opens or closes the expanded part.</summary>
    internal void ToggleSkillDetails() => ArgumentNullException.ThrowIfNull(Entry);
}
