namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The shell's part of the Work page's Closed fold (<see cref="ShellViewModel"/>, continued): the fold reads through the API the shell follows.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>Opens or closes the Closed fold under the Work table.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the fold shows.</returns>
    internal Task ToggleClosed(CancellationToken cancellationToken) =>
        Queue.LastUrl is { } url ? Queue.ToggleClosed(url, cancellationToken) : Task.CompletedTask;
}
