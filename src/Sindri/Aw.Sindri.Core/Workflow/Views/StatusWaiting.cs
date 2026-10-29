using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>A launch waiting for a free session slot (sandbox spec §6, SB-020): how many sessions run and the most that may.</summary>
/// <param name="Running">The sessions running.</param>
/// <param name="Cap">The cap.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StatusWaiting(int Running, int Cap);
