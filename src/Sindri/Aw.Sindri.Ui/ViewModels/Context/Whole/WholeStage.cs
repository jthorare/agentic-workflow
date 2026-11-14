namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>What the stage of an opened area shows (#3674): a canvas of labelled nodes, or the ranked list.</summary>
internal enum WholeStage
{
    /// <summary>At most <see cref="WholeDrillIn.CanvasNodeCap"/> nodes, every one labelled, with their links.</summary>
    Canvas,

    /// <summary>More than that: the ranked list, never edges.</summary>
    List,
}
