namespace Aw.Sindri.Features.LinearConnection;

/// <summary>What a key save did.</summary>
/// <param name="Saved">Always true: a failed save is an error.</param>
/// <param name="ProfileHash">The live profile's hash.</param>
/// <param name="Write">For a write key, where it stands; null for a read key.</param>
internal sealed record SaveLinearConnectionResponse(bool Saved, string ProfileHash, LinearSavedWriteView? Write);
