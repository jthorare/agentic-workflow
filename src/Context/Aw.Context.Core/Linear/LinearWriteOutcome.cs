namespace Aw.Context.Core.Linear;

/// <summary>
/// How a write that was sent ended: <see cref="LinearWriteApplied"/>, <see cref="LinearWriteAppliedNotStored"/> or <see cref="LinearWriteUnknown"/>. A refusal (nothing was sent, or Linear said no) is a Result error with its SND-LINEAR code, not one of these.
/// </summary>
public abstract record LinearWriteOutcome
{
    private protected LinearWriteOutcome()
    {
    }
}
