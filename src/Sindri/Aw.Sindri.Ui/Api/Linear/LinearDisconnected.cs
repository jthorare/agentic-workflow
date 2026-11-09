namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>200 of DELETE /sindri/linear/connection/write.</summary>
/// <param name="Removed">True.</param>
/// <param name="ProfileHash">The live profile's hash.</param>
internal sealed record LinearDisconnected(bool Removed, string ProfileHash);
