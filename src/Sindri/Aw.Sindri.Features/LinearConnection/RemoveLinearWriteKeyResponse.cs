namespace Aw.Sindri.Features.LinearConnection;

/// <summary>What the removal did.</summary>
/// <param name="Removed">Always true: a removal that could not happen is an error.</param>
/// <param name="ProfileHash">The live profile's hash.</param>
internal sealed record RemoveLinearWriteKeyResponse(bool Removed, string ProfileHash);
