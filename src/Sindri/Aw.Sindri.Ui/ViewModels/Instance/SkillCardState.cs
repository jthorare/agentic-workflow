namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The words a skill card shows.</summary>
internal enum SkillCardState
{
    /// <summary>"waiting to start".</summary>
    WaitingToStart = 1,

    /// <summary>"running".</summary>
    Running = 2,

    /// <summary>"waiting on you".</summary>
    WaitingOnYou = 3,

    /// <summary>"done".</summary>
    Done = 4,

    /// <summary>"failed".</summary>
    Failed = 5,
}
