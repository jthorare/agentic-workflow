using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Core.Todos.Note;
using Aw.Sindri.Core.Views;
using Aw.Kernel.Ids;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Features.SyncTodoNote;

/// <summary>What one sync pass reaches: the files, the note's place, the disk, the ledger and views, the picks in flight, and the clock and ids.</summary>
/// <param name="Files">The note, tmp, intent and base files.</param>
/// <param name="Locator">Finds the note.</param>
/// <param name="Disk">Probes whether the folder can swap.</param>
/// <param name="Ledger">The ledger the events go to.</param>
/// <param name="Held">The lease this process holds.</param>
/// <param name="Events">Builds the entries.</param>
/// <param name="Views">The todos.</param>
/// <param name="Holds">The open hold.</param>
/// <param name="Picks">The picks still inside their Undo window.</param>
/// <param name="Scrubber">Scrubs the text read from the note.</param>
/// <param name="Clock">Sindri's clock.</param>
/// <param name="Ids">Mints ids.</param>
/// <param name="Logger">The pass's log line.</param>
internal sealed record SyncDeps(
    ITodoNoteFiles Files,
    INoteLocator Locator,
    ITodoNoteDisk Disk,
    ILedgerStore Ledger,
    HeldLease Held,
    EventFactory Events,
    IViewReader Views,
    TodoHolds Holds,
    TodoPickWindow Picks,
    ITextScrubber Scrubber,
    TimeProvider Clock,
    IIdGenerator Ids,
    ILogger Logger);
