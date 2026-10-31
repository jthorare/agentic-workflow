namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One node of the whole map.</summary>
/// <param name="Key">The node's key.</param>
/// <param name="Label">The label, scrubbed.</param>
/// <param name="Kind">The node kind.</param>
/// <param name="Area">The node's primary area.</param>
/// <param name="AlsoIn">The other areas it sits in.</param>
/// <param name="Trust">trusted or untrusted.</param>
/// <param name="Provenance">Where it came from.</param>
/// <param name="Confidence">How sure the map is (0 to 1).</param>
/// <param name="Confirmed">True when a person confirmed it.</param>
/// <param name="Repo">The repo it belongs to, or null (left out of the answer) when it belongs to no single repo.</param>
/// <param name="Owner">True when the owner named it in the profile (pages spec §3.5 "Set by a person"); left out of the answer when false.</param>
internal sealed record WholeNode(string Key, string Label, string Kind, string Area, IReadOnlyList<string> AlsoIn, string Trust, string Provenance, double Confidence, bool Confirmed, string? Repo = null, bool Owner = false);
