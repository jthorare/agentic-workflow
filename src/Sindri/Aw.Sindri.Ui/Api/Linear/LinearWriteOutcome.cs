namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>
/// What a Linear write came to (Linear hub 1b contract §1 to §3): applied (<see cref="LinearChangeApplied"/>, <see cref="LinearCommentPosted"/>,
/// <see cref="LinearCreated"/>), a conflict on an edit (<see cref="LinearChangeConflict"/>), unknown (<see cref="LinearWriteUnknown"/>) or
/// rate-limited (<see cref="LinearWriteRateLimited"/>). Any other refusal is the call's error.
/// </summary>
internal abstract record LinearWriteOutcome;
