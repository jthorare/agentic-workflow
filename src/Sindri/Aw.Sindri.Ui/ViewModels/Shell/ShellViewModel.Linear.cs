#pragma warning disable CA1822, MA0041 // why: the red stub reads nothing yet
using Aw.Sindri.Ui.ViewModels.Linear;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Work page's Linear view in the shell (<see cref="ShellViewModel"/>, continued; Linear hub spec §3).</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The Work page's Linear view: its switch, list and since-you-looked strip.</summary>
    public LinearWorkViewModel Linear => throw new NotSupportedException();
}
