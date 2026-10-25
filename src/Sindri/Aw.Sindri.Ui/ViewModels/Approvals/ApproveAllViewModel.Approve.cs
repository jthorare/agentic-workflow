namespace Aw.Sindri.Ui.ViewModels.Approvals;

/// <summary>The approve-all dialog (<see cref="ApproveAllViewModel"/>, continued): the one click that approves.</summary>
internal sealed partial class ApproveAllViewModel
{
    /// <summary>Approves the plan: one plain call with no challenge, sent once.</summary>
    /// <returns>A task that completes when the answer, and the job it started, is done.</returns>
    internal Task Approve() => CanApprove ? Redeem(new Api.Workflow.ApproveAllRequest()) : Task.CompletedTask;

    private void ResetAnswerState()
    {
        Result = null;
        Progress = string.Empty;
    }
}
