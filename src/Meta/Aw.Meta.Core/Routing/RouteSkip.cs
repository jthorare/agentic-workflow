namespace Aw.Meta.Core.Routing;

/// <summary>A lane that is running out but cannot be moved, and why.</summary>
/// <param name="Lane">The lane name.</param>
/// <param name="Reason">Why no switch is suggested.</param>
public sealed record RouteSkip(string Lane, string Reason);
