using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'workflow.notify' payload (issue 2258, PR 6): a 'notify' node posted a note, which waits as a needs-you row until its run ends.</summary>
/// <param name="Run">The run.</param>
/// <param name="Node">The notify node.</param>
/// <param name="Title">What the row says.</param>
/// <param name="Body">More text, or null.</param>
/// <param name="By">Who posted it, as the envelope's actor reads.</param>
public sealed record WorkflowNotifyPayload(WorkflowRunId Run, NodeId Node, string Title, string? Body, string By);
