using Aw.Sindri.Ui.Api.Sessions;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>A session that ended recently (collector spec CU-1): what it was, when it ended and the command that resumes it, with Copy.</summary>
internal sealed class EndedSessionViewModel : ObservableObject
{
    private readonly IClipboard _clipboard;
    private readonly TimeProvider _clock;
    private int _copies;

    internal EndedSessionViewModel(EndedSessionView ended, IClipboard clipboard, TimeProvider clock)
    {
        (_clipboard, _clock) = (clipboard, clock);
        Update(ended);
    }

    /// <summary>The key the page's selection uses; distinct from a live row's ref.</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>The session's ref.</summary>
    public string Ref { get; private set; } = string.Empty;

    /// <summary>The provider and title.</summary>
    public string TitleText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The status word of a session that ended.</summary>
    public string StatusText { get; } = Labels.EndedStatus;

    /// <summary>"ended 4m ago".</summary>
    public string EndedText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The resume command, its arguments joined by spaces (an argument with whitespace or a quote is single-quoted), or "no resume command".</summary>
    public string ResumeText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The text Copy puts on the clipboard; empty when there is no resume command.</summary>
    public string ResumeCommand { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when there is a command to copy.</summary>
    public bool CanCopy => ResumeCommand.Length > 0;

    /// <summary>"Copy", or "Copied" for a moment.</summary>
    public string CopyLabel { get; private set { Set(ref field, value); OnChanged(nameof(CopyName)); } } = Labels.Copy;

    /// <summary>The Copy button's accessible name.</summary>
    public string CopyName => string.Equals(CopyLabel, Labels.Copied, StringComparison.Ordinal) ? Labels.Copied : Labels.Copy + ": " + Labels.RecentlyEnded + " " + TitleText;

    /// <summary>The row's accessible name.</summary>
    public string Name { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the row is where the keys act.</summary>
    public bool IsSelected { get; set => Set(ref field, value); }

    /// <summary>Takes a newer serving of the same ended session.</summary>
    /// <param name="ended">The entry as served.</param>
    internal void Update(EndedSessionView ended)
    {
        Ref = ended.Session.Ref;
        Key = "ended/" + ended.Session.Ref;
        var id8 = ended.Session.Id.Length > 8 ? ended.Session.Id[..8] : ended.Session.Id;
        TitleText = ended.Session.ProviderLabel + " · " + (ended.Session.Title is { Length: > 0 } title ? title : ended.Session.Repo is { Length: > 0 } repo ? repo + "/" + id8 : id8);
        EndedText = Labels.EndedAgo(_clock.GetUtcNow() - ended.EndedAt);
        ResumeCommand = ended.Resume is { Count: > 0 } argv && !argv.Any(a => a.Any(char.IsControl)) ? string.Join(' ', argv.Select(Quote)) : string.Empty;
        ResumeText = ResumeCommand.Length > 0 ? ResumeCommand : Labels.NoResumeCommand;
        Name = TitleText + ", " + EndedText + ", " + ResumeText;
        OnChanged(nameof(CanCopy));
        OnChanged(nameof(CopyName));
    }

    /// <summary>Copies the resume command: "Copied" for 1.5 s.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that ends once the clipboard answered.</returns>
    internal async Task Copy(CancellationToken cancellationToken)
    {
        if (!CanCopy || !await _clipboard.SetText(ResumeCommand, cancellationToken).ConfigureAwait(true))
        {
            return;
        }

        CopyLabel = Labels.Copied;
        _ = Uncopy(++_copies, cancellationToken);
    }

    // "Copied" shows for a moment, then the label rests; the copy itself has already finished.
    private async Task Uncopy(int copy, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(ControlViewModel.CopiedFor, _clock, cancellationToken).ConfigureAwait(true);
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

    // POSIX single quotes, only around an argument that holds whitespace or a quote (an embedded ' becomes '\'').
    private static string Quote(string argument) =>
        argument.Any(c => char.IsWhiteSpace(c) || c is '\'' or '"') ? "'" + argument.Replace("'", "'\\''", StringComparison.Ordinal) + "'" : argument;
}
