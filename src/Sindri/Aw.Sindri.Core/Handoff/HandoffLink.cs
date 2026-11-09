namespace Aw.Sindri.Core.Handoff;

/// <summary>A link that travels with an offer or a result. The text is untrusted: it is fenced wherever it reaches a prompt.</summary>
/// <param name="Label">What the link is called.</param>
/// <param name="Url">Where it points (absolute, http or https).</param>
public sealed record HandoffLink(string Label, string Url);
