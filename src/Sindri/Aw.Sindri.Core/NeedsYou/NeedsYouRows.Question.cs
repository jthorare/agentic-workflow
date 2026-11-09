using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Core.NeedsYou;

public static partial class NeedsYouRows
{
    /// <summary>The row for an 'ask_owner' node's question: it takes a text answer and waits until the question is answered or its run ends.</summary>
    /// <param name="question">The 'workflow.question' payload.</param>
    /// <param name="since">When the question was recorded.</param>
    /// <param name="scrubber">Scrubs the text.</param>
    /// <returns>The row.</returns>
    public static Result<NeedsYouRow> ForWorkflowQuestion(WorkflowQuestionPayload question, DateTimeOffset since, ITextScrubber scrubber) => throw new NotSupportedException();
}
