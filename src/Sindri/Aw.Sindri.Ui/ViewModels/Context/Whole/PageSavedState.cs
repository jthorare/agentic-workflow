namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>How a <see cref="PageSave"/> ended.</summary>
internal enum PageSavedState
{
    /// <summary>Written and approved.</summary>
    Approved,

    /// <summary>Written, not approved: it waits for Review and approve (other changes wait too, or the button read Save).</summary>
    Waiting,

    /// <summary>The profile moved since the sheet read it (SND-PROFILE-003): nothing written, every field kept.</summary>
    Clash,

    /// <summary>The write was refused (SND-CONTEXT-250..256): <see cref="PageSaved.Message"/> says why.</summary>
    Refused,

    /// <summary>The write couldn't be sent or its job failed.</summary>
    Failed,
}
