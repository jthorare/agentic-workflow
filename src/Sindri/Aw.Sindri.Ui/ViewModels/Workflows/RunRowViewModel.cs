using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One run on a workflow's runs list: status as a glyph and a word, trigger, revision, when, and why it failed.</summary>
internal sealed class RunRowViewModel : ObservableObject
{
    internal RunRowViewModel(WorkflowRunRowView row, TimeProvider clock)
    {
        Id = row.Id;
        var (glyph, word) = Labels.WfChip(row.Status);
        ChipText = glyph + " " + word;
        Tone = RunStatus.Tone(row.Status);
        (ChipWord, ChipGlyph, ChipToneWord) = (word, glyph, RunStatus.ToneWord(row.Status));
        TriggerText = Labels.WfTriggerWord(row.Trigger);
        RevisionText = Labels.WfRunRevision(row.Revision);
        var live = RunStatus.IsLive(row.Status);
        WhenText = live
            ? Labels.JobWhen(running: true, clock.GetUtcNow() - row.StartedAt)
            : Labels.JobWhen(running: false, clock.GetUtcNow() - (row.EndedAt ?? row.StartedAt));
        ErrorText = row.Error ?? string.Empty;
        Name = Labels.WfRunRowName(Id, word.ToLowerInvariant(), TriggerText.ToLowerInvariant(), WhenText, ErrorText);
    }

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

    /// <summary>The chip's word without its glyph.</summary>
    public string ChipWord { get; }

    /// <summary>The chip's glyph.</summary>
    public string ChipGlyph { get; }

    /// <summary>The chip's StateChipView tone: ok, running, waiting, error or neutral.</summary>
    public string ChipToneWord { get; }

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
