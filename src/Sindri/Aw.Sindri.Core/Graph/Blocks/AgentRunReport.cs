using System.Text.Json.Nodes;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>What one finished agent session hands back to its node (issue 2258, PR 7).</summary>
/// <param name="Fields">The agent's own report: the fields the node declared in 'outputs'.</param>
/// <param name="Status">How the session ended, such as 'succeeded'.</param>
/// <param name="Branch">The branch the session worked on ('wf/&lt;slug&gt;/&lt;short run&gt;'), or null when it made none.</param>
/// <param name="Commit">The head commit of that branch, or null.</param>
/// <param name="SessionId">The launch of the session, the key the app attaches by.</param>
/// <param name="Usage">Tokens and cost as JSON, or null.</param>
public sealed record AgentRunReport(JsonObject Fields, string Status, string? Branch, string? Commit, LaunchId SessionId, JsonObject? Usage);
