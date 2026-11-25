namespace Aw.Sindri.Ui.Api;

/// <summary>GET /sindri/profile (api #782): the live profile's hash and the hash of the file on disk, which the page sheet checks before it writes. The rest of the answer is not read.</summary>
/// <param name="Live">The live profile's hash at the last check, or null.</param>
/// <param name="FileHash">The hash of the profile file on disk when the server answered (#1826); null when it can't be read or from a server before the field.</param>
/// <param name="WorkflowEditor">The profile's workflow.editor, sheet or steps; null when unset (the app treats null as sheet) or from a server before the field.</param>
internal sealed record ProfileRead(string? Live = null, string? FileHash = null, string? WorkflowEditor = null);
