using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One execution of one node: status as a glyph and a word, then input, output, the cut-short note and the error as served.</summary>
internal sealed class NodeRunViewModel : ObservableObject
{
    private readonly Func<NodeRunViewModel, bool, CancellationToken, Task>? _control;

    internal NodeRunViewModel(NodeRunView run, int count, Func<NodeRunViewModel, bool, CancellationToken, Task>? control = null)
    {
        _control = control;
        TitleText = count > 1 || run.ItemIndex > 0 ? Labels.WfItemTitle(run.ItemIndex, count) : run.Node;
        var (glyph, word) = Labels.WfChip(run.Status);
        ChipText = glyph + " " + word;
        Tone = RunStatus.Tone(run.Status);
        (ChipWord, ChipGlyph, ChipToneWord) = (word, glyph, RunStatus.ToneWord(run.Status));
        InputText = WorkflowJson.Pretty(run.Input);
        OutputText = WorkflowJson.Pretty(run.Output);
        IsTruncated = run.Truncated;
        ErrorText = run.Error ?? string.Empty;
        Name = Labels.WfNodeRunName(TitleText, word.ToLowerInvariant(), ErrorText);
        (Node, ItemIndex, Agent) = (run.Node, run.ItemIndex, run.Agent);
    }

    /// <summary>The node's id (the take over call names it).</summary>
    public string Node { get; }

    /// <summary>The item's position, 0 for a node that runs once.</summary>
    public int ItemIndex { get; }

    /// <summary>As served: null, "waiting" or "taken_over".</summary>
    public string? Agent { get; internal set { Set(ref field, value); OnChanged(nameof(CanTakeOver)); OnChanged(nameof(CanHandBack)); } }

    /// <summary>True on an agent node that waits on its agent: Take over shows.</summary>
    public bool CanTakeOver => string.Equals(Agent, "waiting", StringComparison.Ordinal);

    /// <summary>True on a taken-over agent node: Hand back shows.</summary>
    public bool CanHandBack => string.Equals(Agent, "taken_over", StringComparison.Ordinal);

    /// <summary>True while a take over or hand back call is on the way; a second press does nothing.</summary>
    public bool IsBusy { get; internal set { Set(ref field, value); OnChanged(nameof(TakeOverText)); OnChanged(nameof(HandBackText)); } }

    /// <summary>The Take over button's words: "Take over", or "Taking over…" while its call is on the way.</summary>
    public string TakeOverText => IsBusy ? Labels.WfTakeOverBusy : Labels.WfTakeOver;

    /// <summary>The Hand back button's words: "Hand back", or "Handing back…" while its call is on the way.</summary>
    public string HandBackText => IsBusy ? Labels.WfHandBackBusy : Labels.WfHandBack;

    /// <summary>
    /// Take over: sends the node and item once, notes what Sindri said in the run's notice and reads the run again (a refusal shows the server's text and reads nothing).
    /// </summary>
    /// <param name="cancellationToken">Cancels the call and the read.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task TakeOver(CancellationToken cancellationToken) => _control?.Invoke(this, true, cancellationToken) ?? Task.CompletedTask;

    /// <summary>Hand back: the same, for a taken-over node.</summary>
    /// <param name="cancellationToken">Cancels the call and the read.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task HandBack(CancellationToken cancellationToken) => _control?.Invoke(this, false, cancellationToken) ?? Task.CompletedTask;

    /// <summary>The node's id, or "Item 2 of 3" for one item of a for_each node.</summary>
    public string TitleText { get; }

    /// <summary>The status: glyph and word.</summary>
    public string ChipText { get; }

    /// <summary>The chip's tone; the glyph and word carry the status too.</summary>
    public ChipTone Tone { get; }

    /// <summary>The chip's word without its glyph.</summary>
    public string ChipWord { get; }

    /// <summary>The chip's glyph.</summary>
    public string ChipGlyph { get; }

    /// <summary>The chip's StateChipView tone: ok, running, waiting, error or neutral.</summary>
    public string ChipToneWord { get; }

    /// <summary>True for a succeeded or running node (the ok colour).</summary>
    public bool IsOk => Tone == ChipTone.Ok;

    /// <summary>True for a waiting node (the attention colour).</summary>
    public bool IsAttention => Tone == ChipTone.Attn;

    /// <summary>True for a failed node (the error colour).</summary>
    public bool IsError => Tone == ChipTone.Fail;

    /// <summary>The interpolated config the block got, as indented JSON; empty for none.</summary>
    public string InputText { get; }

    /// <summary>True when the node has an input.</summary>
    public bool HasInput => InputText.Length > 0;

    /// <summary>What the block produced, as indented JSON; empty for none.</summary>
    public string OutputText { get; }

    /// <summary>True when the node has an output.</summary>
    public bool HasOutput => OutputText.Length > 0;

    /// <summary>True when Sindri cut the input or the output to fit its size limit.</summary>
    public bool IsTruncated { get; }

    /// <summary>The note shown when it was cut.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string TruncatedText => Labels.WfTruncated;

    /// <summary>The input's heading.</summary>
    public string InputHeading => Labels.WfInputHeading;

    /// <summary>The output's heading.</summary>
    public string OutputHeading => Labels.WfOutputHeading;
#pragma warning restore CA1822, MA0041

    /// <summary>Why it failed, as served; empty otherwise.</summary>
    public string ErrorText { get; }

    /// <summary>True when the node failed with a reason.</summary>
    public bool HasError => ErrorText.Length > 0;

    /// <summary>The accessible name: title, status word and error.</summary>
    public string Name { get; }
}
