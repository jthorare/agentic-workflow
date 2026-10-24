namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>The approve-all hold an approval belongs to (setup-once spec §7.5): what each 'recipe.approved' of the hold carries beside the recipe.</summary>
/// <param name="Batch">The command id of the hold's start.</param>
/// <param name="PlanDigest">The plan's digest.</param>
/// <param name="HeldMs">How long the owner held the confirmation for, or null when it was typed.</param>
public sealed record ApprovalBatch(string Batch, string PlanDigest, long? HeldMs);
