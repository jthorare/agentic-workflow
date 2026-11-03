using Aw.Evidence.Core.Judging;

namespace Aw.Evidence.Testing;

/// <summary>
/// A scripted IJudge: a wired judge answers from queues (null when empty: the call failed) and records every request;
/// <see cref="Unavailable"/> is no judge at all (IsAvailable false; called anyway, it records and answers null).
/// </summary>
/// <param name="available">Whether a judge is wired.</param>
public sealed class FakeJudge(bool available = true) : IJudge
{
    private readonly Queue<SelectorRepair?> _repairs = new();
    private readonly Queue<VisualVerdict?> _verdicts = new();
    private readonly List<SelectorRepairRequest> _repairRequests = [];
    private readonly List<VisualCritiqueRequest> _critiqueRequests = [];

    /// <summary>No judge wired.</summary>
    public static FakeJudge Unavailable => new(available: false);

    /// <inheritdoc/>
    public bool IsAvailable { get; } = available;

    /// <summary>What <see cref="JevImagesOnAsync"/> answers (default off).</summary>
    public bool JevImages { get; set; }

    /// <summary>The repair requests received.</summary>
    public IReadOnlyList<SelectorRepairRequest> RepairRequests => _repairRequests;

    /// <summary>The critique requests received.</summary>
    public IReadOnlyList<VisualCritiqueRequest> CritiqueRequests => _critiqueRequests;

    /// <summary>Queues repair answers (a null answer is a failed call).</summary>
    /// <param name="answers">The answers, in order.</param>
    /// <returns>This judge.</returns>
    public FakeJudge Repairs(params SelectorRepair?[] answers)
    {
        foreach (var answer in answers)
        {
            _repairs.Enqueue(answer);
        }

        return this;
    }

    /// <summary>Queues critique answers (a null answer is a failed call).</summary>
    /// <param name="answers">The answers, in order.</param>
    /// <returns>This judge.</returns>
    public FakeJudge Critiques(params VisualVerdict?[] answers)
    {
        foreach (var answer in answers)
        {
            _verdicts.Enqueue(answer);
        }

        return this;
    }

    /// <inheritdoc/>
    public ValueTask<bool> JevImagesOnAsync(CancellationToken cancellationToken) => ValueTask.FromResult(JevImages);

    /// <inheritdoc/>
    public ValueTask<SelectorRepair?> RepairSelectorAsync(SelectorRepairRequest request, CancellationToken cancellationToken)
    {
        _repairRequests.Add(request);
        return ValueTask.FromResult(IsAvailable && _repairs.Count > 0 ? _repairs.Dequeue() : null);
    }

    /// <inheritdoc/>
    public ValueTask<VisualVerdict?> CritiqueAsync(VisualCritiqueRequest request, CancellationToken cancellationToken)
    {
        _critiqueRequests.Add(request);
        return ValueTask.FromResult(IsAvailable && _verdicts.Count > 0 ? _verdicts.Dequeue() : null);
    }
}
