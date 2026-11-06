namespace Aw.Sindri.Core.Reviews;

/// <summary>A library folder that couldn't be read.</summary>
/// <param name="Root">The folder as the profile names it.</param>
/// <param name="Reason">Why it couldn't be read.</param>
public sealed record ReviewUnreadableRoot(string Root, string Reason);
