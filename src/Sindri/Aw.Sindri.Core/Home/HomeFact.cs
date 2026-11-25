namespace Aw.Sindri.Core.Home;

/// <summary>One typed fact on a row (home spec §3.1).</summary>
/// <param name="Kind">What the fact says.</param>
/// <param name="Text">The fact's text, scrubbed.</param>
public sealed record HomeFact(HomeFactKind Kind, string Text);
