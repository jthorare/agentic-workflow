using Aw.Context.Core.Linking;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Intent;

/// <summary>One rule's links of a record, replaced without touching its other links or the map version it was linked under (concept pins spec §3.4).</summary>
internal sealed partial class SqliteIntentStore
{
    public ValueTask<Result<Unit>> ReplaceRuleLinks(RecordKey record, LinkRule rule, IReadOnlyList<IntentLink> links, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Q9: not built yet");
}
