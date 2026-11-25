namespace Aw.Sindri.Infrastructure.Connections;

/// <summary>One action of a rest preset: a request the call_tool block can send with the connection's token.</summary>
/// <param name="Name">The action name a node's 'tool' field uses.</param>
/// <param name="Description">What it does.</param>
/// <param name="Method">GET or POST.</param>
/// <param name="Host">The provider host the token may go to.</param>
/// <param name="Path">The path; '{name}' parts are filled from the arguments of the same name.</param>
/// <param name="BareKey">True when the provider wants the key without 'Bearer '.</param>
internal sealed record RestAction(string Name, string Description, HttpMethod Method, string Host, string Path, bool BareKey = false);
