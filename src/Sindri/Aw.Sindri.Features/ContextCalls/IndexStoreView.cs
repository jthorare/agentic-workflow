namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The context.db store row of 'sindri doctor' (context:store), as the index status carries it: read on every status call, whether or not a profile names a feed.</summary>
/// <param name="State">"ok", "warn" or "fail".</param>
/// <param name="Detail">What the check found, e.g. "context.db 2.4 GB, its log 12.0 MB"; on a failed read, "&lt;code&gt;: &lt;message&gt;".</param>
/// <param name="Fix">What to do about it, or null when nothing is needed.</param>
public sealed record IndexStoreView(string State, string Detail, string? Fix);
