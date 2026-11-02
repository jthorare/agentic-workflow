using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>A timing line of a status: how long one Step input of the open run took to fill.</summary>
/// <param name="Input">The input block.</param>
/// <param name="DurationMs">The fill's wall-clock milliseconds.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StatusTiming(string Input, long DurationMs);
