namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>One row of the inspector's "Where it comes from" table.</summary>
/// <param name="Source">The synced source it came from, or who found it when no synced source holds it.</param>
/// <param name="When">When the source was last read, in words (empty when the node has no synced source).</param>
/// <param name="Sure">How sure the map is, in words.</param>
internal sealed record SourceRowViewModel(string Source, string When, string Sure);
