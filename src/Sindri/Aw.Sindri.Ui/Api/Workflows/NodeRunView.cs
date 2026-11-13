using System.Text.Json;

namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>One execution of one node of a run.</summary>
/// <param name="Node">The node id.</param>
/// <param name="ItemIndex">The item's position, 0 for a node that runs once; a for_each node has one per item.</param>
/// <param name="Status">running, waiting, succeeded, failed, skipped or cancelled (read as a string).</param>
/// <param name="Input">The interpolated config the block got, or null.</param>
/// <param name="Output">What the block produced, or null.</param>
/// <param name="Truncated">True when Sindri cut the input or the output to fit its size limit.</param>
/// <param name="Error">Why it failed, or null.</param>
/// <param name="Agent">Null for a node that is not a run_agent waiting on an agent, else "waiting" or "taken_over".</param>
/// <param name="Raw">The raw text a prompt node got back from the model, cut at 64 KB, or null.</param>
internal sealed record NodeRunView(string Node, int ItemIndex, string Status, JsonElement? Input, JsonElement? Output, bool Truncated, string? Error, string? Agent = null, string? Raw = null);
