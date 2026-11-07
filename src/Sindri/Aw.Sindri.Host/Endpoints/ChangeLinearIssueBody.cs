using System.Text.Json;
using Aw.Sindri.Features.LinearIssues;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/linear/issues/{key}/change: only the changed fields, under the same names in both. A part left out is empty.</summary>
/// <param name="Opened">The values the sheet opened with.</param>
/// <param name="Set">The new values (null clears assigneeId, projectId, cycleId and parentKey).</param>
internal sealed record ChangeLinearIssueBody(Dictionary<string, JsonElement>? Opened, Dictionary<string, JsonElement>? Set)
{
    /// <summary>The request for the issue <paramref name="key"/> names.</summary>
    /// <param name="key">The route's issue key.</param>
    /// <returns>The request.</returns>
    internal ChangeLinearIssueRequest ToRequest(string key) => new(key, Opened ?? [], Set ?? []);
}
