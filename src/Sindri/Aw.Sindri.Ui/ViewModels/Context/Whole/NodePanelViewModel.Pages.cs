#pragma warning disable MA0041, CA1822 // red-first stubs: they throw until the implementation commit

using Aw.Kernel;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The sections of an action's or a page's panel (<see cref="NodePanelViewModel"/>, continued, pages spec §3.4).</summary>
internal sealed partial class NodePanelViewModel
{
    /// <summary>Reads the intent lens around one node; null while the page has no connection. Set by the page.</summary>
    internal Func<string, CancellationToken, Task<Result<LensesView>?>>? WhyLoader { get; set; }

    /// <summary>Gets the action or page sections; empty for every other kind.</summary>
    public IReadOnlyList<NodeSectionViewModel> Sections => throw new NotSupportedException();
}
