using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.OpenTargets;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Core.NeedsYou;

public static partial class NeedsYouRows
{
    /// <summary>The row for a graph 'notify' node's note: information, so it has no action; it waits until its run ends.</summary>
    /// <param name="note">The 'workflow.notify' payload.</param>
    /// <param name="since">When the note was recorded.</param>
    /// <param name="scrubber">Scrubs the text.</param>
    /// <returns>The row.</returns>
    public static Result<NeedsYouRow> ForWorkflowNotify(WorkflowNotifyPayload note, DateTimeOffset since, ITextScrubber scrubber)
    {
        ArgumentNullException.ThrowIfNull(note);
        ArgumentNullException.ThrowIfNull(scrubber);
        var cli = CliForm.Parse("sindri ledger");   // why: no verb shows one graph run until PR 8, and a form takes no bare value after a flag, so the row points at the ledger read
        if (cli.IsError)
        {
            return cli.Error;
        }

        return new NeedsYouRow(
            NeedsYouRowIds.WorkflowNotify(note.Run, note.Node),
            NeedsYouKind.WorkflowNotify,
            "workflow · note",
            new NeedsYouSubject(SubjectKind.WorkflowRun, RunSubject(note.Run)),
            Item: null,
            SafeText.Clean(scrubber, note.Title, TitleMax),
            SafeText.Clean(scrubber, note.Body ?? string.Empty, ReasonMax),
            Repo: null,
            Urgency.Pulse,
            since,
            RecordedOnly: false,
            Notice: null,
            AnswerKind: null,
            PrimaryAction: null,
            [],
            cli.Value.Text,
            new NeedsYouOpen(NeedsYouRoutes.WorkflowRun(note.Run), "Open the run"));
    }

    /// <summary>The subject id of a run's rows: the run id with its '@' written as '.', because a subject id holds no '@'. A slug holds no '.', so the result stays unique and the mapping reverses.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The subject id, for example 'nightly.3:12'.</returns>
    internal static SubjectId RunSubject(WorkflowRunId run) => SubjectId.From(run.Value.Replace('@', '.'));
}
