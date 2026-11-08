namespace Aw.Sindri.Features.LinearConnection;

/// <summary>What a key save did.</summary>
/// <param name="Saved">Always true: a failed save is an error.</param>
/// <param name="Approved">True when the live profile is approved after the save: the key was the only waiting change. False when other changes wait (the owner reviews those in Settings).</param>
/// <param name="ProfileHash">The live profile's hash.</param>
/// <param name="Write">For a write key, where it stands; null for a read key.</param>
internal sealed record SaveLinearConnectionResponse(bool Saved, bool Approved, string ProfileHash, LinearSavedWriteView? Write);
