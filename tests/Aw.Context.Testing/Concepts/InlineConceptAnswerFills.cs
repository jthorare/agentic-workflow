using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;

namespace Aw.Context.Testing.Concepts;

/// <summary>The publish fill run inside the publish call, so a test that counts map loads or writes to a fake after an import never races a background fill.</summary>
/// <param name="fill">Builds and keeps the answers for a publisher's scope.</param>
public sealed class InlineConceptAnswerFills(Func<ContextScope, CancellationToken, Task> fill) : IConceptAnswerFills
{
    /// <inheritdoc/>
    public void Start(ContextScope scope) => fill(scope, CancellationToken.None).GetAwaiter().GetResult();   // the fakes answer at once, so this never waits on a thread
}
