using Aw.Evidence.Core.Judging;
using Xunit;

namespace Aw.Evidence.Testing;

/// <summary>The IJudge contract for a judge that isn't wired: it says so, and called anyway it answers nothing.</summary>
public abstract class JudgeContract
{
    /// <summary>Creates a judge that isn't wired.</summary>
    /// <returns>The judge.</returns>
    protected abstract IJudge CreateUnavailable();

    /// <summary>An unwired judge reports it isn't available and answers null to both calls.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_unwired_judge_says_so_and_answers_nothing()
    {
        var judge = CreateUnavailable();

        Expect.That(!judge.IsAvailable, "a judge that isn't wired says so, so callers record no model call");
        Expect.That(await judge.RepairSelectorAsync(new SelectorRepairRequest("open", "click open", []), CancellationToken.None) is null, "no repair");
        Expect.That(await judge.CritiqueAsync(new VisualCritiqueRequest("/runs/r/after.png", BaselineScreenshot: null, "/runs/r"), CancellationToken.None) is null, "no critique");
        Expect.That(!await judge.JevImagesOnAsync(CancellationToken.None), "no judge sends no images to Jev");
    }
}
