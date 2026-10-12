namespace Aw.Sindri.Core.Control;

/// <summary>Where the running control plane listens, as it records it for clients (control.json).</summary>
/// <param name="Url">The base URL, http://127.0.0.1:port.</param>
/// <param name="Pid">The process id.</param>
/// <param name="Epoch">Its lease epoch; a client checks it against the status answer, so a stale file reads as not running.</param>
public sealed record ControlEndpoint(Uri Url, int Pid, long Epoch);
