namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>The icon of each run state (F3 spec 3 §3): the name of a mark in Theme/Icons.axaml, shown together with the state's word.</summary>
internal static class RunStateIcons
{
    /// <summary>The icon of a state.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The mark's name, for StateIcon.Icon.</returns>
    internal static string Of(RunNodeState state) => state switch
    {
        RunNodeState.Running => "Spinner",
        RunNodeState.Done => "Ok",
        RunNodeState.Failed or RunNodeState.Cancelled => "Close",
        RunNodeState.DidntWorkOut => "RunCircleMinus",
        RunNodeState.Waiting or RunNodeState.Paused => "RunClock",
        RunNodeState.TookOver => "RunPerson",
        RunNodeState.NotStarted => "RunCircleDashed",
        _ => "RunCircleDashed",
    };
}
