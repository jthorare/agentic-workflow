namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>The hooks of the flat Row (#3189). Stubs until the slices land; the red tests read them.</summary>
internal sealed partial class Row
{
    /// <summary>What the row drew now.</summary>
    internal RowLayout Layout => throw new NotSupportedException($"The flat Row {Name} is not built yet (#3189).");

    /// <summary>Runs what a render-scaling change runs, since the headless platform can't change the scaling itself.</summary>
    internal void ScalingChangedForTest() => throw new NotSupportedException($"The flat Row {Name} is not built yet (#3189).");
}
