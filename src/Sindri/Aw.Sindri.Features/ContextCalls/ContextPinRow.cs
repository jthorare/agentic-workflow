namespace Aw.Sindri.Features.ContextCalls;

/// <summary>One pin of <see cref="ContextPinsView"/>.</summary>
/// <param name="Id">The pin's id (given, or derived from its name).</param>
/// <param name="Name">The name.</param>
/// <param name="Summary">The definition line, or null.</param>
/// <param name="Aliases">The words people use for it.</param>
/// <param name="Features">The feature keys it lists.</param>
/// <param name="FolderRules">How many paths, dependsOn and owns entries it has.</param>
/// <param name="SetIn">'profile' or 'repo &lt;name&gt;'.</param>
/// <param name="State">'approved' (the live entry is the approved one), 'waiting' (new or changed since approval) or 'removing' (approved, gone from the live profile).</param>
/// <param name="Change">'created', 'updated' or 'removed': how the approved entry differs from the pin the current map was built with; null when the map has it. Always null while the map's only record of its rules is one hash (see <see cref="ContextRebuildView.Stale"/>).</param>
internal sealed record ContextPinRow(string Id, string Name, string? Summary, IReadOnlyList<string> Aliases, IReadOnlyList<string> Features, int FolderRules, string SetIn, string State, string? Change);
