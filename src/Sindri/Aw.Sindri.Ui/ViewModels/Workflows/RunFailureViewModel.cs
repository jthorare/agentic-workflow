using Aw.Sindri.Ui.Api.Workflows;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Components;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// A failed run's header (#3304, spec §7.3): "Failed at {step}", Sindri's sentence, the fix (a command fix copies the command, the way the app's other Copy buttons do) and the engine's text under Details.
/// Every string is Sindri's; the app builds no sentence of its own.
/// </summary>
internal sealed class RunFailureViewModel : ObservableObject
{
    private readonly string? _command;

    internal RunFailureViewModel(StepFailureView failure, string detail, IClipboard? clipboard, TimeProvider clock)
    {
        _command = failure.FixCommand;
        throw new NotSupportedException();
    }

    /// <summary>"Failed at Check".</summary>
    public string HeadingText { get; } = string.Empty;

    /// <summary>Sindri's sentence.</summary>
    public string SentenceText { get; } = string.Empty;

    /// <summary>The copy fix: its label is the fix's words, then "Copied"; null when the fix has no command or there is no clipboard.</summary>
    public CopyAction? Fix { get; }

    /// <summary>True when the fix copies a command.</summary>
    public bool HasCopyFix => Fix is not null;

    /// <summary>The heading over the engine's text.</summary>
    public string DetailsHeading { get; } = string.Empty;

    /// <summary>The engine's text, as served.</summary>
    public string DetailText { get; } = string.Empty;

    /// <summary>The header's accessible name: heading and sentence.</summary>
    public string Name { get; } = string.Empty;

    /// <summary>Copies the fix's command.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the clipboard answered.</returns>
    internal Task CopyFix(CancellationToken cancellationToken) => Fix is { } fix && _command is { } command ? fix.Run(command, cancellationToken) : Task.CompletedTask;
}
