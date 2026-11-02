namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The bands of the map, as F3's context page draws them: the concepts, why we are doing this, what it touches in the product, what it touches in the code.</summary>
internal enum WholeBand
{
    /// <summary>Concepts: drawn in their own row above the why when the map has any, never among the features (the served answer's concept band, #1281).</summary>
    Concept,

    /// <summary>Decisions and records.</summary>
    Intent,

    /// <summary>Features, surfaces, actions, things and flows.</summary>
    Product,

    /// <summary>Code files.</summary>
    Code,

    /// <summary>Any kind the table does not know: drawn in its own row after the code, so nothing is hidden.</summary>
    Other,
}
