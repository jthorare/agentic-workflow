namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>POST /sindri/library/roots' body (review page spec §6.3): a vault to read design docs from.</summary>
/// <param name="Name">Its name: lowercase letters, digits and "-".</param>
/// <param name="Path">The folder.</param>
/// <param name="Deny">Patterns it doesn't read.</param>
/// <param name="Approve">True to approve the profile change when it is the only waiting one.</param>
internal sealed record LibraryRootAdd(string Name, string Path, IReadOnlyList<string> Deny, bool Approve);
