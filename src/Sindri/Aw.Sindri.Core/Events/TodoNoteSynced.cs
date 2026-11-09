namespace Aw.Sindri.Core.Events;

/// <summary>The 'todo.note.synced' payload (todo spec §4.1): a sync pass that changed something.</summary>
/// <param name="Vault">The vault's name.</param>
/// <param name="Note">The note's vault-relative path.</param>
/// <param name="Hash">The sha256 of what Sindri wrote, or of what it read when it wrote nothing.</param>
/// <param name="BaseHash">The hash that names the base file this pass saved (§4.5 The base).</param>
/// <param name="Read">How many task lines the pass read.</param>
/// <param name="Wrote">How many lines it wrote.</param>
/// <param name="By">Always 'service:todo-note'.</param>
public sealed record TodoNoteSynced(string Vault, string Note, string Hash, string BaseHash, int Read, int Wrote, string By);
