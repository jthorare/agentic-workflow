using Aw.Context.Core.Engine;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;

namespace Aw.Context.Features.LinearWriteThrough;

/// <summary>Store one Linear issue's current state right after a write changed it.</summary>
/// <param name="Scope">The caller's scope; its settings name the Linear source.</param>
/// <param name="IssueId">Linear's id of the issue.</param>
/// <param name="Written">The assignee and state fields the write itself sent: a change in one of them is logged as 'write-through', every other difference the re-read finds as 'feed'.</param>
internal sealed record LinearWriteThroughRequest(ContextScope Scope, ExternalId IssueId, LinearWrittenFields Written);
