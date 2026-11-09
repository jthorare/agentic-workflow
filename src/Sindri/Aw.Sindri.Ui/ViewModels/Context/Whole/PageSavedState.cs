namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>How a <see cref="PageSave"/> ended.</summary>
internal enum PageSavedState
{
    /// <summary>Written: the profile applies on save.</summary>
    Applied,

    /// <summary>The profile moved since the sheet read it (SND-PROFILE-003): nothing written, every field kept.</summary>
    Clash,

    /// <summary>The write was refused (SND-CONTEXT-250..256): <see cref="PageSaved.Message"/> says why.</summary>
    Refused,

    /// <summary>The write couldn't be sent or its job failed.</summary>
    Failed,
}
