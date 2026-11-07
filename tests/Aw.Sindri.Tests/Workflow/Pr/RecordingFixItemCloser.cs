using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Tests.Workflow.Pr;

/// <summary>Remembers the fix items the comment loop asked to close; answers as set by <see cref="Answer"/>.</summary>
internal sealed class RecordingFixItemCloser : IFixItemCloser
{
    public List<string> Closed { get; } = [];

    public bool Answer { get; set; } = true;

    public ValueTask<bool> Close(string item, CancellationToken cancellationToken)
    {
        Closed.Add(item);
        return ValueTask.FromResult(Answer);
    }
}
