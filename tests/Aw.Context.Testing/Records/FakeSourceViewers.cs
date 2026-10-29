using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Testing.Records;

/// <summary>
/// The <see cref="ISourceViewers"/> over one scripted viewer: it answers a Linear viewer only for settings that configure Linear, as the real one does, and counts the calls.
/// A test sets <see cref="Answer"/> (a user, or an error to fail with).
/// </summary>
public sealed class FakeSourceViewers : ISourceViewers, ISourceViewer
{
    private int _calls;

    /// <summary>What the viewer answers.</summary>
    public Result<SourceViewer> Answer { get; set; } = new SourceViewer(ExternalId.From("00000000-0000-0000-0000-0000000000a1"), "Ada Owner");

    /// <summary>How many times the viewer was asked.</summary>
    public int Calls => Volatile.Read(ref _calls);

    /// <inheritdoc />
    public ISourceViewer? ForLinear(ContextSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Linear is null ? null : this;
    }

    /// <inheritdoc />
    public ValueTask<Result<SourceViewer>> Viewer(CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _calls);
        return ValueTask.FromResult(Answer);
    }
}
