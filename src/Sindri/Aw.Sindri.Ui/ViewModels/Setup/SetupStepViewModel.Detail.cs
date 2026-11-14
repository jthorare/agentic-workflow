namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>Where a step's own fix sits in its body (#3610).</summary>
internal sealed partial class SetupStepViewModel
{
    /// <summary>True when the fix sits under the link, above the meter, the repo lines and the preview: every step that hasn't failed.</summary>
    public bool HasDetailAbove => HasDetail && !IsFailed;

    /// <summary>True when the fix sits under the red band, so what broke reads before what to do: a failed step.</summary>
    public bool HasDetailBelow => HasDetail && IsFailed;
}
