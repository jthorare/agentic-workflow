using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>A gate on a transition (spec §3.5): '{ use, threshold?, with? }', run in order; the short form is the bare id.</summary>
/// <param name="Use">The gate block.</param>
/// <param name="Threshold">The score a rung-2 or rung-3 gate must reach (W4; RCP-THRESH-001/002).</param>
/// <param name="With">Its arguments by parameter name.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GateUse(BlockId Use, double? Threshold, IReadOnlyDictionary<string, WithValue> With);
