namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The question first (<see cref="ContextPageViewModel"/>, continued): the files the engine returned for the last question, shown as a list (#2974).</summary>
internal sealed partial class ContextPageViewModel
{
    /// <summary>The files the last resolved question returned, in the order the answer gave them.</summary>
    public IReadOnlyList<AnswerFile> AnswerFiles { get; private set; } = [];

    /// <summary>Whether the list has files to show.</summary>
    public bool HasAnswerFiles => AnswerFiles.Count > 0;

    /// <summary>A plain sentence for a stage that draws no nodes; empty when the stage draws something.</summary>
    public string StageMessage { get; private set; } = string.Empty;

    /// <summary>Whether the stage says something.</summary>
    public bool HasStageMessage => StageMessage.Length > 0;
}
