namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>How a <see cref="ProfileWriteFlow.Run"/> ended.</summary>
internal enum ProfileWriteState
{
    /// <summary>Written and approved.</summary>
    Approved,

    /// <summary>Written, not approved: it waits for Review and approve.</summary>
    Waiting,

    /// <summary>The profile moved since the sheet read it (SND-PROFILE-003): nothing written.</summary>
    Clash,

    /// <summary>The write was refused with a sentence of the server's (SND-CONTEXT-…): <see cref="ProfileWritten.Message"/> says why.</summary>
    Refused,

    /// <summary>The write couldn't be sent, its job failed, or it ran past the wait.</summary>
    Failed,
}
