using Aw.Sindri.Core.Todos.Note;

namespace Aw.Sindri.Features.CheckTodoNote;

/// <summary>What the note holds and what the disk allows (todo spec §3.4, §4.4).</summary>
/// <param name="Vault">The vault checked.</param>
/// <param name="Note">The note path checked.</param>
/// <param name="Exists">Whether the note is there; false: Sindri creates it.</param>
/// <param name="Open">Open task lines, those that can't import included.</param>
/// <param name="Done">Done task lines.</param>
/// <param name="Other">Lines that aren't top-level task lines.</param>
/// <param name="CantImport">The first five open task lines that break the todo text rules, each with its line number and a short reason ("markdown link", "due marker inside the text").</param>
/// <param name="WillImport">How many open lines an import would adopt after the open todos limit, beside the todos Sindri already has.</param>
/// <param name="Cap">The most open todos Sindri holds (500).</param>
/// <param name="SindriOpen">Open todos Sindri already holds.</param>
/// <param name="CanSwap">Whether the disk swaps two files in one step; false: Sindri can read the note but not write new lines into it.</param>
public sealed record CheckTodoNoteResponse(string Vault, string Note, bool Exists, int Open, int Done, int Other, IReadOnlyList<NoteCantImport> CantImport, int WillImport, int Cap, int SindriOpen, bool CanSwap);
