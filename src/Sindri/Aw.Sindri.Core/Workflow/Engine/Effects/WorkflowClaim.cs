namespace Aw.Sindri.Core.Workflow.Engine.Effects;

/// <summary>An item's claim (engine spec §5.7, §12 'workflow_claims (item, owner instance, attempt)' in ledger.db; engine P6 unit (d)).</summary>
/// <param name="Item">The claimed item.</param>
/// <param name="Owner">The instance that holds it.</param>
/// <param name="Attempt">The owner's attempt.</param>
public sealed record WorkflowClaim(ItemKey Item, InstanceAddress Owner, int Attempt);
