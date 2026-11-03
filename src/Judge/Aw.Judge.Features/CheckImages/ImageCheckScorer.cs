using System.Text.Json.Nodes;
using Aw.Judge.Core.Images;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Questions;

namespace Aw.Judge.Features.CheckImages;

/// <summary>Asks one provider every case directly, with the same per-call image folder a live call gives the agent CLIs.</summary>
/// <param name="workspaces">Makes the per-call image folders.</param>
/// <param name="clock">Times the calls.</param>
internal sealed class ImageCheckScorer(IImageWorkspaces workspaces, TimeProvider clock)
{
    private const string LooksRight = "looks-right";

    public async ValueTask<ImageCheckTally> Score(IModelProvider provider, IReadOnlyList<ImageCase> set, CancellationToken cancellationToken)
    {
        var tally = new ImageCheckTally();
        foreach (var item in set)
        {
            var image = ImageCheckCases.Attachment(item);
            using var scope = new ImageScope(workspaces, [image], ImageCheckCases.Root);
            var visual = new VisualCritiqueInput(item.Name + ".png", ImageCheckCases.Root);
            var question = VisualCritiqueQuestion.Definition.ToRef(visual) with { Images = [image], ImageScope = scope };
            var input = new JsonObject { ["afterScreenshot"] = visual.AfterScreenshot, ["evidenceDir"] = ImageCheckCases.Root };
            var start = clock.GetTimestamp();
            var answer = await provider.Decide(question, input, VisualCritiqueQuestion.Definition.TimeBudget, cancellationToken).ConfigureAwait(false);
            var judgedRight = answer is ProviderDecided decided && string.Equals(decided.Decision, LooksRight, StringComparison.Ordinal);
            tally.Add(string.Equals(item.Expected, LooksRight, StringComparison.Ordinal), judgedRight, clock.GetElapsedTime(start));
        }

        return tally;
    }
}
