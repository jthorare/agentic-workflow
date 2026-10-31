namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One pin of <see cref="PinsView"/>.</summary>
/// <param name="Id">The pin's id (given, or derived from the name).</param>
/// <param name="Name">The name.</param>
/// <param name="Summary">The definition line, or null.</param>
/// <param name="Aliases">The words people use for it.</param>
/// <param name="Features">The exact feature keys it lists.</param>
/// <param name="FolderRules">How many of its paths, dependsOn and owns entries are set (outside the app).</param>
/// <param name="SetIn">"profile", or "repo &lt;name&gt;" for a repo's own entry.</param>
/// <param name="State">One of <see cref="PinStates"/>: how the live entry stands against the approved profile.</param>
/// <param name="Change">"created", "updated" or "removed": the last write of this pin that the current map wasn't built with; null when the map has it.</param>
internal sealed record PinView(string Id, string Name, string? Summary, IReadOnlyList<string> Aliases, IReadOnlyList<string> Features, int FolderRules, string SetIn, string State, string? Change);
