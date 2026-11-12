using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One run on a workflow's runs list: status as a glyph and a word, trigger, revision, when, and why it failed.</summary>
internal sealed class RunRowViewModel : ObservableObject
{
    private readonly WorkflowRunRowView _row;
    private readonly TimeProvider _clock;
    private readonly string _word;

    internal RunRowViewModel(WorkflowRunRowView row, TimeProvider clock)
    {
        (_row, _clock) = (row, clock);
        Id = row.Id;
        var (glyph, word) = Labels.WfChip(row.Status);
        ChipText = glyph + " " + word;
        Tone = RunStatus.Tone(row.Status);
        TriggerText = Labels.WfTriggerWord(row.Trigger);
        RevisionText = Labels.WfRunRevision(row.Revision);
        _word = word.ToLowerInvariant();
        WhenText = When();
        ErrorText = row.Error ?? string.Empty;
        Name = RowName();
    }

    /// <summary>Words "Started 2m ago" or "Ended 5m ago" again from the clock now (the shell's one-second beat calls it).</summary>
    internal void Age()
    {
        WhenText = When();
        Name = RowName();
    }

    /// <summary>True while the run has not ended.</summary>
    internal bool IsLive => RunStatus.IsLive(_row.Status);

    private string When() => IsLive
        ? Labels.JobWhen(running: true, _clock.GetUtcNow() - _row.StartedAt)
        : Labels.JobWhen(running: false, _clock.GetUtcNow() - (_row.EndedAt ?? _row.StartedAt));

    private string RowName() => Labels.WfRunRowName(Id, _word, TriggerText.ToLowerInvariant(), WhenText, ErrorText);

    /// <summary>The run's id.</summary>
    public string Id { get; }

    /// <summary>The status: glyph and word.</summary>
    public string ChipText { get; }

    /// <summary>The chip's tone; the glyph and word carry the status too.</summary>
    public ChipTone Tone { get; }

    /// <summary>True for a succeeded or running run (the ok colour).</summary>
    public bool IsOk => Tone == ChipTone.Ok;

    /// <summary>True for a waiting run (the attention colour).</summary>
    public bool IsAttention => Tone == ChipTone.Attn;

    /// <summary>True for a failed run (the error colour).</summary>
    public bool IsError => Tone == ChipTone.Fail;

    /// <summary>"Manual", "Schedule".</summary>
    public string TriggerText { get; }

    /// <summary>"Revision 1".</summary>
    public string RevisionText { get; }

    /// <summary>"Started 2m ago" while it is live, "Ended 5m ago" after.</summary>
    public string WhenText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Why it failed, as served; empty otherwise.</summary>
    public string ErrorText { get; }

    /// <summary>True when the run has an error.</summary>
    public bool HasError => ErrorText.Length > 0;

    /// <summary>The row's accessible name.</summary>
    public string Name { get; private set => Set(ref field, value); } = string.Empty;
}
