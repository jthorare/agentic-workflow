using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.OpenTargets;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Core.NeedsYou;

public static partial class NeedsYouRows
{
    /// <summary>
    /// The row for an 'ask_owner' node's question: it takes a text answer and waits until the question is answered, its node ends or its run ends.
    /// Its reason lists the choices and, when the node has a deadline and a default, what it answers if nobody does.
    /// </summary>
    /// <param name="question">The 'workflow.question' payload.</param>
    /// <param name="since">When the question was recorded.</param>
    /// <param name="scrubber">Scrubs the text.</param>
    /// <returns>The row.</returns>
    public static Result<NeedsYouRow> ForWorkflowQuestion(WorkflowQuestionPayload question, DateTimeOffset since, ITextScrubber scrubber)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(scrubber);
        var cli = CliForm.Parse(question.Item is null ? "sindri answer <run>/<node> [--text <text>] [--choice <choice>]" : "sindri answer <run>/<node> --item <item> [--text <text>] [--choice <choice>]");   // why: the form is a template (a run id holds an '@', which a filled form refuses); the reason names the real run and node
        if (cli.IsError)
        {
            return cli.Error;
        }

        var reason = new List<string> { $"Answer {question.Run.Value}/{question.Node.Value}" + (question.Item is { } item ? $" item {item.ToString(System.Globalization.CultureInfo.InvariantCulture)}." : ".") };
        if (question.Choices.Count > 0)
        {
            reason.Add("Choices: " + string.Join(", ", question.Choices) + ".");
        }

        if (question.TimeoutAt is { } deadline)
        {
            reason.Add($"If nobody answers by {deadline.UtcDateTime.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture)} UTC it " + (question.Default is { } fallback ? $"answers '{fallback}'." : "answers with nothing."));
        }

        return new NeedsYouRow(
            NeedsYouRowIds.WorkflowQuestion(question.Run, question.Node, question.Item),
            NeedsYouKind.Question,
            "workflow · question",
            new NeedsYouSubject(SubjectKind.WorkflowRun, RunSubject(question.Run)),
            Item: null,
            SafeText.Clean(scrubber, question.Question, TitleMax),
            SafeText.Clean(scrubber, string.Join(' ', reason), ReasonMax),
            Repo: null,
            Urgency.Colour,
            since,
            RecordedOnly: false,
            Notice: null,
            AnswerKind.Text,
            PrimaryAction: null,
            [],
            cli.Value.Text,
            new NeedsYouOpen(NeedsYouRoutes.WorkflowRun(question.Run), "Open the run"));
    }
}
