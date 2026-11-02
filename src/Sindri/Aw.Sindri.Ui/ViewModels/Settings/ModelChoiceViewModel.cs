using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Settings;

/// <summary>
/// Settings' read-only model-choice line (model-pick spec 8): on, off, or "the profile has a change waiting for your approval", from what Sindri serves in the status (the app derives none of it),
/// the sentence under it, and a Copy of the three lines that change it (the profile file's path, 'sindri profile validate', 'sindri profile approve'). There is no edit verb.
/// </summary>
internal sealed class ModelChoiceViewModel : ObservableObject
{
    private static readonly TimeSpan _copiedFor = TimeSpan.FromMilliseconds(1500);
    private readonly IClipboard _clipboard;
    private readonly TimeProvider _clock;
    private int _copies;

    /// <summary>The line over the status's profile part, with a clipboard and a clock for Copy's "Copied".</summary>
    /// <param name="clipboard">Where Copy puts the three lines.</param>
    /// <param name="clock">The clock for "Copied".</param>
    internal ModelChoiceViewModel(IClipboard clipboard, TimeProvider clock)
    {
        _clipboard = clipboard;
        _clock = clock;
    }

    /// <summary>The heading.</summary>
#pragma warning disable CA1822, MA0041 // why: the view binds it as an instance property of the view model
    public string Title => Labels.ModelChoiceTitle;

    /// <summary>The sentence under the line.</summary>
    public string Sentence => Labels.ModelChoiceSentence;
#pragma warning restore CA1822, MA0041

    /// <summary>The line in words; the off words until the first status.</summary>
    public string Line { get; private set => Set(ref field, value); } = Labels.ModelChoiceOff;

    /// <summary>The three lines Copy puts on the clipboard.</summary>
    public string CopyText { get; private set => Set(ref field, value); } = Labels.ModelChoiceValidate + "\n" + Labels.ModelChoiceApprove;

    /// <summary>"Copy", or "Copied" for 1.5 s after a copy.</summary>
    public string CopyLabel
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(CopyName));
        }
    } = Labels.Copy;

    /// <summary>The Copy button's accessible name: "Copy: Model choice", then "Copied".</summary>
    public string CopyName => string.Equals(CopyLabel, Labels.Copied, StringComparison.Ordinal) ? Labels.Copied : Labels.Copy + ": " + Labels.ModelChoiceTitle;

    /// <summary>A status arrived: the line follows its profile part.</summary>
    /// <param name="profile">The status's profile part.</param>
    internal void Show(ProfileView profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Line = profile.NeedsApproval ? Labels.ModelChoiceWaiting : string.Equals(profile.ModelPick, "on", StringComparison.Ordinal) ? Labels.ModelChoiceOn : Labels.ModelChoiceOff;
        CopyText = string.Join('\n', new[] { profile.Source, Labels.ModelChoiceValidate, Labels.ModelChoiceApprove }.Where(static l => !string.IsNullOrEmpty(l)));
    }

    /// <summary>Copies the three lines: "Copied" for 1.5 s.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the clipboard answered.</returns>
    internal async Task Copy(CancellationToken cancellationToken)
    {
        if (!await _clipboard.SetText(CopyText, cancellationToken).ConfigureAwait(true))
        {
            return;
        }

        CopyLabel = Labels.Copied;
        _ = Uncopy(++_copies, cancellationToken);
    }

    private async Task Uncopy(int copy, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_copiedFor, _clock, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (copy == _copies)
        {
            CopyLabel = Labels.Copy;
        }
    }
}
