using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One execution of one node: status as a glyph and a word, then input, output, the cut-short note and the error as served.</summary>
internal sealed class NodeRunViewModel : ObservableObject
{
    internal NodeRunViewModel(NodeRunView run, int count)
    {
        TitleText = count > 1 || run.ItemIndex > 0 ? Labels.WfItemTitle(run.ItemIndex, count) : run.Node;
        var (glyph, word) = Labels.WfChip(run.Status);
        ChipText = glyph + " " + word;
        InputText = WorkflowJson.Pretty(run.Input);
        OutputText = WorkflowJson.Pretty(run.Output);
        IsTruncated = run.Truncated;
        ErrorText = run.Error ?? string.Empty;
        Name = Labels.WfNodeRunName(TitleText, word.ToLowerInvariant(), ErrorText);
    }

    /// <summary>The node's id, or "Item 2 of 3" for one item of a for_each node.</summary>
    public string TitleText { get; }

    /// <summary>The status: glyph and word.</summary>
    public string ChipText { get; }

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
