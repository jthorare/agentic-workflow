namespace Aw.Sindri.Ui.Api;

/// <summary>The body of POST /sindri/todos/note/import (todo spec §4.4): the note and the open count the card showed, so a note that gained or lost a task since is refused with SND-TODO-409.</summary>
/// <param name="Vault">The vault's name.</param>
/// <param name="Note">The note's vault-relative path.</param>
/// <param name="ExpectOpen">The open task count Check note showed.</param>
internal sealed record TodoNoteImportBody(string Vault, string Note, int ExpectOpen);
