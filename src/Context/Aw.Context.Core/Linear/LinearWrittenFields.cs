namespace Aw.Context.Core.Linear;

/// <summary>The fields a write-through itself sent to Linear, of the two a change row records (spec 2026-11-04 §4.1): a difference in one of these is the write's own, any other is a teammate's.</summary>
[Flags]
public enum LinearWrittenFields
{
    /// <summary>None: a tick's page, or a write that sent neither.</summary>
    None = 0,

    /// <summary>The write set the assignee.</summary>
    Assignee = 1,

    /// <summary>The write set the workflow state.</summary>
    State = 2,
}
