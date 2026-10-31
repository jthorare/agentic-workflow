namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>A concept's pages and actions (<see cref="WholeMapViewModel"/>, continued, pages spec §3.4): the Why drill lists them after its decisions and records.</summary>
internal sealed partial class WholeMapViewModel
{
    /// <summary>Gets the Pages and actions section.</summary>
    public PagesSectionViewModel Pages { get; } = new();

    /// <summary>Gets a value indicating whether the section shows.</summary>
    public bool ShowPages => HasConcept && false;
}
