namespace Aw.Sindri.Features.CheckTodoNote;

/// <summary>What the note holds and what the disk allows (todo spec §3.4, §4.4).</summary>
/// <param name="Vault">The vault checked.</param>
/// <param name="Note">The note path checked.</param>
/// <param name="Exists">Whether the note is there; false: Sindri creates it.</param>
/// <param name="Open">Open task lines, those that can't import included.</param>
/// <param name="Done">Done task lines.</param>
/// <param name="Other">Lines that aren't top-level task lines.</param>
/// <param name="Importable">Open task lines an import would adopt.</param>
/// <param name="CantImport">Open task lines that break the todo text rules (a markdown link, a due marker inside the text).</param>
/// <param name="CantImportLines">The line numbers of the first five of those.</param>
/// <param name="SindriOpen">Open todos Sindri already holds.</param>
/// <param name="WillImport">How many of <paramref name="Importable"/> fit under the 500 open todos limit beside <paramref name="SindriOpen"/>.</param>
/// <param name="OverCap">Whether <paramref name="WillImport"/> is less than <paramref name="Importable"/>.</param>
/// <param name="CanSwap">Whether the disk swaps two files in one step; false: Sindri can read the note but not write new lines into it.</param>
public sealed record CheckTodoNoteResponse(
    string Vault,
    string Note,
    bool Exists,
    int Open,
    int Done,
    int Other,
    int Importable,
    int CantImport,
    IReadOnlyList<int> CantImportLines,
    int SindriOpen,
    int WillImport,
    bool OverCap,
    bool CanSwap);
