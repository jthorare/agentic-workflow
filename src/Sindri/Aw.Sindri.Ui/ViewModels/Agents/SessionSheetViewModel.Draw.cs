using Aw.Sindri.Ui.Api.Sessions;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Instance;

namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>What an answer becomes on the sheet (<see cref="SessionSheetViewModel"/>, continued): the words of the fields, the host, the history and the pending Focus.</summary>
internal sealed partial class SessionSheetViewModel
{
    private void Draw(SessionView view)
    {
        var session = view.Session;
        Ref = session.Ref;
        Heading = session.ProviderLabel + " " + (session.Title ?? (session.Repo is { } repo ? repo + "/" + Id8(session.Id) : Id8(session.Id)));
        StateLine = StateWords(session);
        Fields = Listed([
            (Labels.ItemRepo, session.Repo),
            (Labels.SheetWorktree, session.Worktree),
            (Labels.SheetLane, session.Lane is { } lane ? lane.Team + " / " + lane.Lane : null),
            (Labels.SheetLaunch, session.LaunchId),
            .. StepFields(session),
            (Labels.SheetStarted, session.StartedAt is { } started ? Labels.SheetTime(started) : null),
            (Labels.SheetSince, Labels.SheetTime(session.Since)),
            (Labels.SheetStateSource, session.StateSource),
            (Labels.CellProcess, session.Pid is { } pid ? Number(pid) : null),
            (Labels.SheetWorkingDirectory, view.Cwd)]);
        var host = session.Host;
        HostFields = Listed(
            (Labels.SheetHost, host.Label),
            (Labels.SheetTerminal, host.Terminal),
            (Labels.SheetSocketKind, host.SocketKind),
            (Labels.SheetTmuxSession, host.Session),
            (Labels.SheetPane, host.Pane),
            (Labels.SheetSocket, host.Socket),
            (Labels.SheetAttachedClients, host.AttachedClients is { } clients ? Number(clients) : null));
        var history = view.StateHistory ?? [];
        History = [.. history.Select((entry, i) => new StateHistoryEntryViewModel(SessionChips.For(entry.State), EntryWords(entry, isNow: i == history.Count - 1), IsNow: i == history.Count - 1))];
        (IsEnded, EndedText, EndedDetail, ProblemText, Note) = (false, string.Empty, string.Empty, string.Empty, string.Empty);
        Phase = FramePhase.Shown;
        DrawSections(session);
        DrawFocus();
    }

    private static List<SessionFieldViewModel> Listed(params (string Label, string? Value)[] fields) =>
        [.. fields.Where(f => !string.IsNullOrWhiteSpace(f.Value)).Select(f => new SessionFieldViewModel(f.Label, f.Value!))];   // why: filtered to non-blank above

    private static string StateWords(SessionRowView session)
    {
        var words = new List<string> { SessionChips.For(session.State).Text };
        if (session.Estimated)
        {
            words.Add(Labels.SheetEstimated);
        }

        if (session.Asking is { } asking)
        {
            words.Add(asking.Reason);
            if (!string.IsNullOrEmpty(asking.Tool))
            {
                words.Add(asking.Tool);
            }
        }

        return string.Join(" · ", words);
    }

    private static string EntryWords(StateHistoryEntryView entry, bool isNow)
    {
        var words = new List<string>();
        if (!string.IsNullOrEmpty(entry.Reason))
        {
            words.Add(entry.Reason);
        }

        words.Add(Labels.SheetTime(entry.At));
        if (isNow)
        {
            words.Add(Labels.SheetNow);
        }

        words.Add(entry.Source);
        return string.Join(" · ", words);
    }

    // Focus is the catalog's control, drawn pending until the send exists (U11a-f): a ready row with no sender reads as pending too, so the sheet never shows a dead button.
    private void DrawFocus()
    {
        Focus = _catalog?.On("FocusSession", string.Empty, send: null);
        if (Focus is not { } focus)
        {
            (FocusLine, FocusCli) = (string.Empty, string.Empty);
            return;
        }

        var cli = focus.Cli.Replace("<ref>", Ref, StringComparison.Ordinal);
        FocusCli = ControlViewModel.Safe(cli) && Ref.Length > 0 ? cli : string.Empty;
        FocusLine = FocusCli.Length > 0 ? Labels.FocusSoon + " · " + FocusCli : string.Empty;
        FocusName = Labels.FocusName;
        OnChanged(nameof(IsFocusEnabled));
    }
}
