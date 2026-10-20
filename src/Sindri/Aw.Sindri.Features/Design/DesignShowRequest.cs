namespace Aw.Sindri.Features.Design;

/// <summary>Build the stop-2 view over the files a caller holds (line spec §6.4; 'sindri design show' and 'plan show').</summary>
/// <param name="Facts">The plan packet's files ('tasks.json', 'holdout.json') with the design packet's under 'design/' and optionally the scope packet's under 'scope/' and 'freezeRefused.json'; a section whose files are absent is empty and named in the view's notYet.</param>
public sealed record DesignShowRequest(IReadOnlyDictionary<string, string> Facts);
