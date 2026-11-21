namespace Aw.Sindri.Core.Home;

/// <summary>A cross-link from a row to a PR, issue, thread, run, session or scope (home spec §3.1).</summary>
/// <param name="Kind">The linked object's kind.</param>
/// <param name="Id">Its id.</param>
/// <param name="Title">Its title, or null.</param>
public sealed record HomeLink(string Kind, string Id, string? Title);
