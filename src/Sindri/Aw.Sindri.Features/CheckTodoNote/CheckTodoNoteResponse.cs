using Aw.Sindri.Core.Todos.Note;

namespace Aw.Sindri.Features.CheckTodoNote;

/// <summary>What the note holds and what the disk allows (todo spec §3.4, §4.4).</summary>
/// <param name="Exists">Whether the note is there; false: Sindri creates it.</param>
/// <param name="Open">Open task lines, those that can't import included.</param>
/// <param name="Done">Done task lines.</param>
/// <param name="Other">Lines that aren't top-level task lines.</param>
/// <param name="CantImport">Every open task line that breaks the todo text rules (a markdown link, a due marker inside the text), with the reason.</param>
/// <param name="WillImport">How many open lines an import would adopt after the open todos limit, beside the todos Sindri already has.</param>
/// <param name="Cap">The most open todos Sindri holds (500).</param>
/// <param name="SindriOpen">Open todos Sindri already holds.</param>
/// <param name="CanSwap">Whether the disk swaps two files in one step; false: Sindri can read the note but not write new lines into it.</param>
public sealed record CheckTodoNoteResponse(bool Exists, int Open, int Done, int Other, IReadOnlyList<NoteCantImport> CantImport, int WillImport, int Cap, int SindriOpen, bool CanSwap);
