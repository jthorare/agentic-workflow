using System.Text.Json.Serialization;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ConnectMessages;

/// <summary>
/// Connect Slack for Messages (POST /sindri/messages/connect, 'sindri messages connect [--manifest]'; messages spec §5.1, §8.2): the owner's only, loopback only.
/// The CLI reads the token at a no-echo prompt and sends it in the body; it is never a flag, never logged and never in the record's text.
/// </summary>
/// <param name="Token">The User OAuth Token ('xoxp-'); null with <paramref name="Manifest"/>. Secret.</param>
/// <param name="Manifest">True: answer the app manifest and store nothing.</param>
public sealed record ConnectMessagesRequest([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Token, bool Manifest) : ICommand<ConnectMessagesResponse>
{
    /// <summary>A fixed text: the token is not in it.</summary>
    /// <returns>'ConnectMessagesRequest(manifest)' or 'ConnectMessagesRequest(token)'.</returns>
    public override string ToString() => Manifest ? "ConnectMessagesRequest(manifest)" : "ConnectMessagesRequest(token)";
}
