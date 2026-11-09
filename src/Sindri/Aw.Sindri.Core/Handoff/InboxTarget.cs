using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Core.Handoff;

/// <summary>Which inbox a call goes to: the connection's base URL, its key and the owner whose offers are listed.</summary>
/// <param name="BaseUrl">The inbox API's base URL. Its host has to be on the client's allowlist.</param>
/// <param name="Credential">The connection's key (its AccessToken is sent as a bearer token). It never prints.</param>
/// <param name="Owner">The owner the offers are for.</param>
public sealed record InboxTarget(Uri BaseUrl, TokenSet Credential, string Owner);
