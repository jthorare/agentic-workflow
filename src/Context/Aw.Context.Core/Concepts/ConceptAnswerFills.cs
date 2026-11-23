using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Concepts;

/// <summary>
/// Runs the publish fill of the kept concepts and glossary answers (<see cref="ConceptAnswers.Fill"/>) after the publish, never inside it: the import or promote returns once its
/// version is committed, and the fill runs on its own, one after another, so a publish job ends and serve starts without waiting for a whole-map read. One per process; a fill a
/// restart cuts short leaves the reads to build what it didn't keep.
/// </summary>
/// <param name="fill">Builds and keeps the answers for a publisher's scope in a scope of its own (the host's registration makes it, so this type needs no container).</param>
/// <param name="failed">Told about a fill that threw (the host logs it), or null.</param>
public sealed class ConceptAnswerFills(Func<ContextScope, CancellationToken, Task> fill, Action<Exception>? failed = null) : IConceptAnswerFills
{
    private readonly Lock _gate = new();
    private Task _last = Task.CompletedTask;

    /// <inheritdoc/>
    public void Start(ContextScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        lock (_gate)
        {
            // a fill outlives the publish that started it, so it takes no caller's token; a fill that throws is reported and the next one still runs
            _last = _last.ContinueWith(_ => fill(scope, CancellationToken.None), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default).Unwrap()
                .ContinueWith(Report, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        }
    }

    private void Report(Task done)
    {
        if (done.Exception is { } thrown)
        {
            failed?.Invoke(thrown.GetBaseException());
        }
    }
}
