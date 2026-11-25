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

    /// <summary>The header of one failed run.</summary>
    /// <param name="failure">The failed node run's failure, as served.</param>
    /// <param name="detail">The engine's text for Details: the run's error, else the node run's.</param>
    /// <param name="clipboard">Where the fix copies its command; null shows no copy button.</param>
    /// <param name="clock">The clock "Copied" times out on.</param>
    internal RunFailureViewModel(StepFailureView failure, string detail, IClipboard? clipboard, TimeProvider clock)
    {
        _command = failure.FixCommand;
        HeadingText = Labels.WfFailedAt(failure.Step);
        SentenceText = failure.Sentence;
        DetailText = detail;
        Name = Labels.WfFailureName(HeadingText, SentenceText);
        (HasOpenFix, OpenText) = (false, string.Empty);   // the label-only fix is not a button yet
        Fix = clipboard is not null && _command is { Length: > 0 } command
            ? new CopyAction(clipboard, clock, (failure.FixLabel ?? Labels.Copy) + " " + command, failure.FixLabel)
            : null;
    }

    /// <summary>"Failed at Check".</summary>
    public string HeadingText { get; }

    /// <summary>Sindri's sentence.</summary>
    public string SentenceText { get; }

    /// <summary>The copy fix: its label is the fix's words, then "Copied"; null when the fix has no command or there is no clipboard.</summary>
    public CopyAction? Fix { get; }

    /// <summary>True when the fix has a label but no command: its button opens the failed step.</summary>
    public bool HasOpenFix { get; }

    /// <summary>The open button's words (the fix's label); empty with no such fix.</summary>
    public string OpenText { get; }

    /// <summary>True when the fix copies a command.</summary>
    public bool HasCopyFix => Fix is not null;

    /// <summary>The heading over the engine's text.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string DetailsHeading => Labels.WfFailureDetails;
#pragma warning restore CA1822, MA0041

    /// <summary>The engine's text, as served.</summary>
    public string DetailText { get; }

    /// <summary>The header's accessible name: heading and sentence.</summary>
    public string Name { get; }

    /// <summary>Copies the fix's command.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the clipboard answered.</returns>
    internal Task CopyFix(CancellationToken cancellationToken) => Fix is { } fix && _command is { } command ? fix.Run(command, cancellationToken) : Task.CompletedTask;
}
