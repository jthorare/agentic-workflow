using Avalonia.Controls;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Views.Context.Whole;

/// <summary>The whole map's edge legend (q86): solid is confirmed, dashed is suggested; <see cref="Extra"/> takes the bands' kind entries.</summary>
internal sealed partial class WholeMapLegend : UserControl
{
    /// <summary>Builds the legend.</summary>
    public WholeMapLegend()
    {
        InitializeComponent();
        WholeLegendSolidText.Text = Labels.ContextMapLegendConfirmed;
        WholeLegendDashedText.Text = Labels.ContextMapLegendSuggested;
    }

    /// <summary>Gets the panel after the two edge entries, where the bands' kind styles go.</summary>
    internal Panel Extra => WholeLegendExtra;
}
