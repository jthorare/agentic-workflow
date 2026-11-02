namespace Aw.Context.Core.Graph;

/// <summary>Where a node came from, as 'context show' words it: the record or file the map stored for it. <paramref name="Url"/> is the record's stored permalink or the file on GitHub, and null when there is none to give.</summary>
/// <param name="Kind">What it is: 'ticket', 'doc', 'file', 'project', 'update', or the record key's scheme for any other source.</param>
/// <param name="Ref">Its id or path ('linear:issue/…', 'repo:aw-split-panes/docs/architecture.md', 'src/layout/SplitPane.ts'), scrubbed.</param>
/// <param name="Title">The record's title when the map holds the record (untrusted, so fenced for a caller that gets fences), else null.</param>
/// <param name="Url">A link to it when the map stores an allowed one, else null.</param>
public sealed record NodeSource(string Kind, string Ref, string? Title = null, string? Url = null);
