namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>The answer of each Library write (add a root, remove one, turn aw on or off).</summary>
/// <param name="Approved">True when the change took effect; false while it waits for the owner's profile approval.</param>
/// <param name="PendingCount">How many profile changes still wait.</param>
internal sealed record LibraryRootChanged(bool Approved, int PendingCount);
