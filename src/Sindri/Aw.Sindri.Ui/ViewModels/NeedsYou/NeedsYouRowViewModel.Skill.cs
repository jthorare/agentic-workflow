namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>A Needs-you row (<see cref="NeedsYouRowViewModel"/>, continued): a `skillWaiting` row's one-line text, its Details and the one-time takeover hint (skill flows spec §5.4).</summary>
internal sealed partial class NeedsYouRowViewModel
{
    private const string NoResult = "noResult";

    private bool _skillDetailsOpen;

    /// <summary>True for a `skillWaiting` row.</summary>
    public bool IsSkillWaiting => Entry.OwnerWaiting is not null && string.Equals(Entry.Kind, NeedsYouKinds.SkillWaiting, StringComparison.Ordinal);

    /// <summary>The row text, cut to one line by the view; empty for any other row.</summary>
    public string SkillLine => IsSkillWaiting ? Entry.Reason : "";

    /// <summary>True when the row offers Details: a skill row whose reason is not `noResult` (it has no question, path or detail worth a second look).</summary>
    public bool CanShowSkillDetails => IsSkillWaiting && !string.Equals(Entry.OwnerWaiting?.Reason, NoResult, StringComparison.Ordinal);

    /// <summary>True while the expanded part is shown.</summary>
    public bool SkillDetailsOpen => CanShowSkillDetails && _skillDetailsOpen;

    /// <summary>"Details" while closed, "Hide details" while open.</summary>
    public string SkillDetailsButtonText => _skillDetailsOpen ? Labels.SkillRowHideDetails : Labels.SkillRowDetails;

    /// <summary>The skill's full question (reason `question`); empty otherwise.</summary>
    public string SkillQuestion => string.Equals(Entry.OwnerWaiting?.Reason, "question", StringComparison.Ordinal) ? Entry.OwnerWaiting?.Text ?? "" : "";

    /// <summary>The result file the reader looked at.</summary>
    public string SkillResultPath => Entry.OwnerWaiting?.ResultPath ?? "";

    /// <summary>The parse error or the other skill's name; empty when none.</summary>
    public string SkillDetail => Entry.OwnerWaiting?.Detail ?? "";

    /// <summary>True when the one-time takeover hint is drawn under this row.</summary>
    public bool ShowsTakeoverHint
    {
        get;
        internal set
        {
            if (field != value)
            {
                field = value;
                OnChanged(nameof(ShowsTakeoverHint));
                OnChanged(nameof(TakeoverHint));
            }
        }
    }

    /// <summary>The one-time hint's words; empty while the hint isn't shown.</summary>
    public string TakeoverHint => ShowsTakeoverHint ? Labels.SkillRowTakeoverHint : "";

    /// <summary>Opens or closes the expanded part; a row without Details ignores it.</summary>
    internal void ToggleSkillDetails()
    {
        if (!CanShowSkillDetails)
        {
            return;
        }

        _skillDetailsOpen = !_skillDetailsOpen;
        OnChanged(nameof(SkillDetailsOpen));
        OnChanged(nameof(SkillDetailsButtonText));
    }
}
