using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One run on a workflow's runs list: status as a glyph and a word, trigger, revision, when, and why it failed.</summary>
internal sealed class RunRowViewModel : ObservableObject
{
    internal RunRowViewModel(WorkflowRunRowView row, TimeProvider clock)
    {
        (Id, Status) = (row.Id, row.Status);
        var (glyph, word) = Labels.WfChip(row.Status);
        Tone = RunStatus.Tone(row.Status);
        (ChipWord, ChipGlyph, ChipToneWord) = (word, glyph, RunStatus.ToneWord(row.Status));
        TriggerText = Labels.WfTriggerWord(row.Trigger);
        RevisionText = Labels.WfRunRevision(row.Revision);
        var live = RunStatus.IsLive(row.Status);
        WhenText = live
            ? Labels.JobWhen(running: true, clock.GetUtcNow() - row.StartedAt)
            : Labels.JobWhen(running: false, clock.GetUtcNow() - (row.EndedAt ?? row.StartedAt));
        ErrorText = row.Error ?? string.Empty;
        var (now, local) = (clock.GetUtcNow(), TimeZoneInfo.ConvertTime(row.StartedAt, clock.LocalTimeZone));
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        StartedAt = row.StartedAt;
        TimeText = Labels.AgoWords(now - row.StartedAt);
        TimeTip = local.ToString("ddd d MMM yyyy", culture) + " \u00b7 " + local.ToString("HH:mm:ss", culture) + "\n" + Labels.WfExactAge(now - row.StartedAt);
        TriggerWords = Labels.WfRunTriggerWords(row.Trigger);
        RevisionShort = string.Create(culture, $"r{row.Revision}");
        var seconds = row.Seconds ?? (row.EndedAt is { } ended && !live ? (ended - row.StartedAt).TotalSeconds : (double?)null);
        DurationText = seconds is { } s and >= 0 ? Labels.WfDuration(s) : string.Empty;
        ResultText = row.Result ?? row.Error ?? string.Empty;
        Name = Labels.WfRunRowName(Id, word.ToLowerInvariant(), TriggerText.ToLowerInvariant(), WhenText, ErrorText);
    }

    /// <summary>The run's id.</summary>
    public string Id { get; }

    /// <summary>The served status: running, waiting, queued, succeeded, failed, skipped or cancelled.</summary>
    public string Status { get; }

    /// <summary>The chip's tone; the glyph and word carry the status too.</summary>
    public ChipTone Tone { get; }

    /// <summary>True for a failed run (the error colour).</summary>
    public bool IsError => Tone == ChipTone.Fail;

    /// <summary>The chip's word without its glyph.</summary>
    public string ChipWord { get; }

    /// <summary>The chip's glyph.</summary>
    public string ChipGlyph { get; }

    /// <summary>The chip's StateChipView tone: ok, running, waiting, error or neutral.</summary>
    public string ChipToneWord { get; }

    /// <summary>The time cell: "22m ago".</summary>
    public string TimeText { get; }

    /// <summary>The time cell's tip, two lines: the date and time, then "7h 42m ago".</summary>
    public string TimeTip { get; }

    /// <summary>The trigger in words: "By hand", "On schedule", "From the CLI", "By an agent".</summary>
    public string TriggerWords { get; }

    /// <summary>"r2".</summary>
    public string RevisionShort { get; }

    /// <summary>"10 s", or empty while it runs.</summary>
    public string DurationText { get; }

    /// <summary>The one-line result.</summary>
    public string ResultText { get; }

    /// <summary>When it started; the date filter reads it.</summary>
    internal DateTimeOffset StartedAt { get; }

    /// <summary>True when the run's tick box is on (Compare takes two).</summary>
    public bool IsSelected { get; set => Set(ref field, value); }

    /// <summary>"Manual", "Schedule".</summary>
    public string TriggerText { get; }

    /// <summary>"Revision 1".</summary>
    public string RevisionText { get; }

    /// <summary>"Started 2m ago" while it is live, "Ended 5m ago" after.</summary>
    public string WhenText { get; }

    /// <summary>Why it failed, as served; empty otherwise.</summary>
    public string ErrorText { get; }

    /// <summary>True when the run has an error.</summary>
    public bool HasError => ErrorText.Length > 0;

    /// <summary>The row's accessible name.</summary>
    public string Name { get; }
}
