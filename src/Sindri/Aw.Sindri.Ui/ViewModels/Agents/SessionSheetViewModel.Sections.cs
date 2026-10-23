using Aw.Sindri.Ui.Api.Sessions;
using Aw.Sindri.Ui.ViewModels.Components;

namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The session sheet's header block and sections (<see cref="SessionSheetViewModel"/>, continued; agents spec §2): how long in its status, the ref's copy chip, the asking callout, and the fields in captioned groups.</summary>
internal sealed partial class SessionSheetViewModel
{
    private static readonly string[] _whereLabels =
    [
        Labels.ItemRepo, Labels.SheetWorktree, Labels.SheetLane, Labels.SheetLaunch, Labels.SheetInstance, Labels.SheetStep, Labels.SheetAttempt, Labels.CellProcess, Labels.SheetWorkingDirectory,
    ];

    private static readonly string[] _timingLabels = [Labels.SheetStarted, Labels.SheetSince, Labels.SheetStateSource];

    /// <summary>The fields in groups: where it is working, then timing; a group with no field is left out. The host has its own list.</summary>
    public IReadOnlyList<SessionSectionViewModel> Sections { get; private set => Set(ref field, value); } = [];

    /// <summary>Asks the Agents page whether a ref is a recently ended session and what resumes it: null when it is not one, empty when it has no resume command, else the command.</summary>
    internal Func<string, string?>? EndedResumeOf { get; set; }

    /// <summary>What the sheet of an ended session says about resuming it; empty for any other sheet.</summary>
    public string EndedResumeText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The Host group's caption.</summary>
    public string HostHeading { get; } = Labels.SheetHost;

    /// <summary>True when the session is asking: the amber callout shows.</summary>
    public bool IsAsking { get; private set => Set(ref field, value); }

    /// <summary>What the callout says.</summary>
    public string CalloutText { get; } = Labels.SheetAskingCallout;

    /// <summary>"In this status for 4m"; empty before the clock is wired.</summary>
    public string InStatusText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The copy chip of the session's ref; null before a session is drawn.</summary>
    public CopyAction? RefCopy { get; private set => Set(ref field, value); }

    /// <summary>Copies the session's ref.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the clipboard answered.</returns>
    internal Task CopyRef(CancellationToken cancellationToken) => RefCopy?.Run(Ref, cancellationToken) ?? Task.CompletedTask;

    // A session that ended: the page knows its resume command (or that it has none) from the row that opened the sheet.
    private void DrawEndedResume() =>
        EndedResumeText = EndedResumeOf?.Invoke(Ref) switch
        {
            null => string.Empty,
            "" => Labels.SheetNoResume,
            var command => Labels.SheetResumeWith(command),
        };

    private void DrawSections(SessionRowView session)
    {
        Sections = Group((Labels.SheetWhereWorking, _whereLabels), (Labels.SheetTiming, _timingLabels));
        IsAsking = string.Equals(session.State, "asking", StringComparison.Ordinal);
        InStatusText = _catalog is null ? string.Empty : Labels.SheetInStatusFor(Labels.Duration((long)Math.Max(0, (_clock.GetUtcNow() - session.Since).TotalSeconds)));
        RefCopy = _catalog is { } catalog ? new CopyAction(catalog.Clipboard, _clock, Labels.SheetCopyRef + " " + session.Ref, Labels.SheetCopyRef) : null;
    }

    private List<SessionSectionViewModel> Group(params (string Heading, string[] Names)[] groups) =>
        [.. groups
            .Select(g => new SessionSectionViewModel(g.Heading, [.. Fields.Where(f => g.Names.Contains(f.Label, StringComparer.Ordinal))]))
            .Where(s => s.Fields.Count > 0)];
}
