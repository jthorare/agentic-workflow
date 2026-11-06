namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>A PR head fetched into the run's refs.</summary>
/// <param name="Branch">The branch it sits on ('sindri/&lt;instance&gt;/&lt;run&gt;/pr').</param>
/// <param name="Head">The sha the branch holds.</param>
internal sealed record FetchedPr(string Branch, string Head);
