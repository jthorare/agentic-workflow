namespace Aw.Sindri.Ui.ViewModels.Rows;

/// <summary>What a group header of RowList shows, on top of the rows an <see cref="IRowGroup"/> holds.</summary>
internal interface IRowGroupHeading : IRowGroup
{
    /// <summary>The heading, in sentence case.</summary>
    public string Heading { get; }

    /// <summary>The right-hand summary, if any.</summary>
    public string? Summary { get; }
}
