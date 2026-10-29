using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Projects;

/// <summary>Which stored projects the Projects page wants facts for (projects page spec §6.1).</summary>
/// <param name="Attached">The Linear project keys the owner attached by hand, in the order the page keeps them; one the store doesn't hold, or the caller can't see, is left out of the answer.</param>
public sealed record ProjectFactsRequest(IReadOnlyList<RecordKey> Attached);
