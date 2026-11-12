using System.Diagnostics.CodeAnalysis;
namespace Aw.Sindri.Ui.ViewModels.Rows;

/// <summary>What a group header of RowList shows, on top of the rows an <see cref="IRowGroup"/> holds.</summary>
[SuppressMessage("Design", "MA0182", Justification = "Stub of the red commit (#2978); the control commit uses it.")]
internal interface IRowGroupHeading : IRowGroup
{
    /// <summary>The heading, in sentence case.</summary>
    public string Heading { get; }

    /// <summary>The right-hand summary, if any.</summary>
    public string? Summary { get; }
}
