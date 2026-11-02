namespace Aw.Sindri.Ui.Api.Sandbox;

/// <summary>POST /sindri/sandbox/allow body (wire contract item 8; human only): a draft profile edit that opens the profile approval.</summary>
/// <param name="Launch">The launch whose denial it fixes (its repo is the repo edited).</param>
/// <param name="Kind">'network' (a host) or 'path'.</param>
/// <param name="Target">The host or path.</param>
internal sealed record AllowRequest(string Launch, string Kind, string Target);
