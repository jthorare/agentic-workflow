using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Concepts;

/// <summary>Starts the publish fill of the kept concepts and glossary answers (issue 3676); <see cref="ConceptAnswerFills"/> runs it after the publish, in the background.</summary>
public interface IConceptAnswerFills
{
    /// <summary>Queues a fill for <paramref name="scope"/> behind the ones already running and returns at once.</summary>
    /// <param name="scope">The publisher's scope.</param>
    public void Start(ContextScope scope);
}
