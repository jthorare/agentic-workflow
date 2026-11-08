using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.LinearConnection;

/// <summary>Saves a pasted Linear key (the read key with its picked teams, or the write key). Owner only. The key goes to the keychain and its pointer to the live profile; it is never logged or put in an error.</summary>
/// <param name="Kind">'read' or 'write'.</param>
/// <param name="Key">The pasted key.</param>
/// <param name="Teams">For a read key, the team keys to read (at least one); ignored for a write key.</param>
internal sealed record SaveLinearConnectionRequest(string Kind, string Key, IReadOnlyList<string>? Teams = null) : ICommand<SaveLinearConnectionResponse>
{
    // why: the generated ToString would print the key into any log that prints the request
    public override string ToString() => $"SaveLinearConnectionRequest {{ Kind = {Kind}, Key = (withheld) }}";
}
