namespace Aw.Sindri.Ui.Api.Home;

/// <summary>A cross-link from a row to a PR, issue, thread, run, session or scope.</summary>
/// <param name="Kind">The linked object's kind.</param>
/// <param name="Id">Its id.</param>
/// <param name="Title">Its title, or null.</param>
internal sealed record HomeLinkView(string Kind, string Id, string? Title);
