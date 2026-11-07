namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>200 of POST /sindri/linear/connection.</summary>
/// <param name="Saved">True.</param>
/// <param name="Approved">False when the profile has other changes waiting (the app links to the approve view).</param>
/// <param name="ProfileHash">The live profile's hash.</param>
/// <param name="Write">For a write key, its state; null for a read key.</param>
internal sealed record LinearConnected(bool Saved, bool Approved, string ProfileHash, LinearWriteState? Write);
