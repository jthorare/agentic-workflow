using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>What an 'ask_owner' node hands on.</summary>
/// <param name="Outputs">'answer' and 'answeredBy'.</param>
/// <param name="Branch">'answered' or 'timeout'.</param>
public sealed record AskOwnerOutcome(JsonObject Outputs, string Branch);
