#pragma warning disable CA1822, IDE0060 // red commit: stub, replaced by the green commit

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): the editor's keyboard map (spec 4.6) wired to the page.</summary>
internal sealed partial class WorkflowsPageViewModel
{
    /// <summary>Does what a key press means on the editor.</summary>
    /// <param name="press">The key and the focus facts.</param>
    /// <param name="cancellationToken">Cancels the calls.</param>
    /// <returns>True when the key was the editor's and is handled.</returns>
    internal Task<bool> PressKey(EditorKeyPress press, CancellationToken cancellationToken) => Task.FromResult(false);
}
