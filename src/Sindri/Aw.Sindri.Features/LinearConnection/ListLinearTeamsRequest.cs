using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.LinearConnection;

/// <summary>Lists the teams a pasted read key can see, so the owner can tick the ones to read. Owner only. The key is held for this one call: it is never logged, stored or put in an error.</summary>
/// <param name="Key">The pasted Linear API key.</param>
internal sealed record ListLinearTeamsRequest(string Key) : ICommand<ListLinearTeamsResponse>
{
    // why: the generated ToString would print the key into any log that prints the request
    public override string ToString() => "ListLinearTeamsRequest { Key = (withheld) }";
}
