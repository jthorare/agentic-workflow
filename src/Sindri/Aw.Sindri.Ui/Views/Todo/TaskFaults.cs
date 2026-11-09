using System.Runtime.ExceptionServices;

namespace Aw.Sindri.Ui.Views.Todo;

/// <summary>Where a key or click starts a task nobody awaits.</summary>
internal static class TaskFaults
{
    /// <summary>A fault is thrown again on the UI thread, as an event handler's would be, instead of being dropped.</summary>
    /// <param name="task">The task a handler started.</param>
    internal static void Rethrow(Task task) =>
        _ = task.ContinueWith(
            static done =>
            {
                if (done.Exception?.GetBaseException() is { } fault)
                {
                    ExceptionDispatchInfo.Throw(fault);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.FromCurrentSynchronizationContext());
}
