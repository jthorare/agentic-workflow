using Aw.Sindri.Ui.ViewModels.Context.Concepts;

namespace Aw.Sindri.Ui.Tests.Context.Concepts;

/// <summary>A sheet under test and how many times it asked to close.</summary>
internal sealed class PinSeveralViewModelBox
{
    internal PinSeveralViewModel? Sheet { get; set; }

    internal int Closed { get; set; }
}
