using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Library;

/// <summary>What one ref resolved to.</summary>
/// <param name="Ref">The ref as written.</param>
/// <param name="Status">How it resolved.</param>
/// <param name="Doc">The doc it names; null unless resolved.</param>
/// <param name="Item">The item it names, as stored; null unless resolved.</param>
public sealed record LibraryRefResolution(string Ref, LibraryRefStatus Status, RecordKey? Doc, LibraryItem? Item);
