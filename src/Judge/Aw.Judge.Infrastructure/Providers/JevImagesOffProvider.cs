using System.Text.Json.Nodes;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>
/// Keeps images away from Jev while sending them is off (config.json's providers.jevImages: false, or AW_JUDGE_JEV_IMAGES=0): an image call
/// is unavailable as 'images-off' and the chain moves to the agent CLIs, while a text call goes to Jev as ever. The catalog adds it only when off.
/// </summary>
/// <param name="inner">The Jev adapter.</param>
internal sealed class JevImagesOffProvider(IModelProvider inner) : IModelProvider
{
    /// <inheritdoc />
    public ProviderName Name => inner.Name;

    /// <inheritdoc />
    public IReadOnlySet<ContentClass> Classes => inner.Classes;

    /// <inheritdoc />
    public async ValueTask<ProviderResult> Decide(QuestionRef question, JsonNode? input, TimeSpan budget, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(question);
        if (question.Images.Count == 0)
        {
            return await inner.Decide(question, input, budget, cancellationToken).ConfigureAwait(false);
        }

        if (!question.ImagesBestEffort)
        {
            return new ProviderUnavailable(ProviderUnavailable.ImagesOff);
        }

        // A hook's call drops its images and goes as text: hooks never sent images before, so turning them off gives today's call.
        var result = await inner.Decide(JevImageDrops.With(question, []), JevImageDrops.With(input, []), budget, cancellationToken).ConfigureAwait(false);
        return result with { ImageNotes = ["image sending to Jev is off (judge config set providers jev-images true); asked jev as text"] };
    }
}
