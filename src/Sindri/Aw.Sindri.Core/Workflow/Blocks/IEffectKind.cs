namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>An effect block (spec §2): builds the outbox command a transition emits. Registered with AddEffect.</summary>
public interface IEffectKind
{
    /// <summary>Builds the command's payload.</summary>
    /// <param name="context">What it builds from.</param>
    /// <param name="cancellationToken">Cancels the build.</param>
    /// <returns>The payload.</returns>
    public ValueTask<EffectPayload> Build(EffectContext context, CancellationToken cancellationToken);
}
