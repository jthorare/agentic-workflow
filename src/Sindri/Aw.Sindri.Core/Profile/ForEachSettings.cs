using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The profile's 'workflow.forEach:' block: the defaults of the 'for_each' block (PR 9).</summary>
/// <param name="MaxItems">The most items a 'for_each' takes unless its node says otherwise, 1 to <see cref="Graph.Blocks.ForEachLimits.MaxItemsCeiling"/>; absent: <see cref="Graph.Blocks.ForEachLimits.DefaultMaxItems"/>.</param>
/// <param name="BatchSize">How many items one child workflow runs, 1 to <see cref="Graph.Blocks.ForEachLimits.BatchSizeCeiling"/>; absent: <see cref="Graph.Blocks.ForEachLimits.DefaultBatchSize"/>.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ForEachSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? MaxItems = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? BatchSize = null);
