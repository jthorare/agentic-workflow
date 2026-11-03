using Aw.Evidence.Core.Judging;
using Microsoft.Extensions.Logging;

namespace Aw.Evidence.Infrastructure.Judging;

#pragma warning disable MA0182 // kept as the reference for an unwired judge until cutover unit 1.3 decides whether the offline fallback needs it
/// <summary>
/// The judge until its CLI adapter lands: <see cref="IsAvailable"/> is false, so callers neither call it nor record a
/// model invocation; selector repair doesn't happen and the visual verdict stays unchecked (callers fail closed). Called
/// anyway, every answer is null. Says once per process, at information, that it isn't wired.
/// </summary>
internal sealed partial class UnavailableJudge(ILogger<UnavailableJudge> logger) : IJudge
{
    private int _announced;

    /// <inheritdoc/>
    public bool IsAvailable
    {
        get
        {
            Announce();
            return false;
        }
    }

    /// <inheritdoc/>
    public ValueTask<bool> JevImagesOnAsync(CancellationToken cancellationToken) => ValueTask.FromResult(false);

    /// <inheritdoc/>
    public ValueTask<SelectorRepair?> RepairSelectorAsync(SelectorRepairRequest request, CancellationToken cancellationToken)
    {
        Announce();
        return ValueTask.FromResult<SelectorRepair?>(null);
    }

    /// <inheritdoc/>
    public ValueTask<VisualVerdict?> CritiqueAsync(VisualCritiqueRequest request, CancellationToken cancellationToken)
    {
        Announce();
        return ValueTask.FromResult<VisualVerdict?>(null);
    }

    private void Announce()
    {
        if (Interlocked.Exchange(ref _announced, 1) == 0)
        {
            NotWired(logger);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The judge adapter is not wired yet: no selector repair, and visual verdicts stay unchecked")]
    private static partial void NotWired(ILogger logger);
}
#pragma warning restore MA0182
