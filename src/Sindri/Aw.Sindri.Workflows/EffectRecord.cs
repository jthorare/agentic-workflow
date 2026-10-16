using Aw.Sindri.Core.Workflow.Engine.Effects;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// One effect row the interpreter writes into the ledger through 'RecordEffect' (engine spec §5.6, §5.7; engine P6 unit
/// (a)). Like <see cref="WorkflowEventRecord"/> it carries no key: the activity keys it by its own ActivityInfo.
/// </summary>
/// <param name="Item">The work item.</param>
/// <param name="Row">The row; its state picks the event type (<see cref="OutboxEvents.Of"/>).</param>
internal sealed record EffectRecord(string Item, EffectRow Row);
