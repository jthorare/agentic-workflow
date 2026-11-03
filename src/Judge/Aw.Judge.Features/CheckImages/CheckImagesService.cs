using System.Globalization;
using Aw.Judge.Core.Config;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Secrets;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.CheckImages;

/// <summary>
/// 'judge check images': whether Jev should judge screenshots, answered by running it. One live visual-critique call through the chain with
/// images forced on, then every labeled case asked of Jev and of claude-cli directly. The bar is zero broken pages judged 'looks-right' by
/// Jev, because nothing after a wrong 'looks-right' looks again. Writes nothing to config.json.
/// </summary>
internal sealed class CheckImagesService(
    IJudgeConfigStore configs, IProviderCatalog catalog, IImageCaseSet cases, ISecretSource secrets, ImageCheckLiveCall live, ImageCheckScorer scorer)
    : IService<CheckImagesRequest, CheckImagesResponse>
{
    public async ValueTask<Result<CheckImagesResponse>> Handle(CheckImagesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await secrets.Read(SecretName.TypeSafeApiKey, cancellationToken).ConfigureAwait(false) is null)
        {
            return Stop("no Jev key: judge check images needs one");
        }

        var set = cases.Cases;
        if (set.Count == 0)
        {
            return Stop("no image cases embedded");
        }

        var reading = ConfigReading.From(await configs.Load(cancellationToken).ConfigureAwait(false));
        var providers = reading.IsSuccess ? reading.Value.Config.Providers : null;
        var jev = catalog.ProviderWithImagesOn(ProviderName.Jev, request.Providers, providers);
        if (jev.IsError)
        {
            return Stop($"jev is not available: {jev.Error.Message}");
        }

        List<string> lines = [.. await live.Run(set, request.Providers, providers, jev.Value, cancellationToken).ConfigureAwait(false)];
        var jevScore = await scorer.Score(jev.Value, set, cancellationToken).ConfigureAwait(false);
        lines.Add(jevScore.Line("jev:"));
        var cli = catalog.Provider(ProviderName.ClaudeCli, request.Providers, providers);
        lines.Add(cli.IsError ? $"{"claude-cli:",-12}not available ({cli.Error.Message})" : (await scorer.Score(cli.Value, set, cancellationToken).ConfigureAwait(false)).Line("claude-cli:"));
        lines.AddRange(Verdict(jevScore, JevImagesSetting.IsOn(providers, request.JevImagesEnvironment)));
        return new CheckImagesResponse(lines, jevScore.BrokenJudgedRight == 0);
    }

    private static Result<CheckImagesResponse> Stop(string line) => new CheckImagesResponse([line], Held: false);

    // The bar, whether images go to Jev now, and the one command that would flip it the way the result says.
    private static List<string> Verdict(ImageCheckTally jev, bool on)
    {
        var held = jev.BrokenJudgedRight == 0;
        List<string> lines =
        [
            held
                ? "bar (0 broken judged looks-right): held"
                : string.Create(CultureInfo.InvariantCulture, $"bar (0 broken judged looks-right): missed ({jev.BrokenJudgedRight}/{jev.Broken})"),
            $"images to Jev: {(on ? "on" : "off")}",
        ];
        if (held && !on)
        {
            lines.Add("to turn it on: judge config set providers jev-images true");
        }
        else if (!held && on)
        {
            lines.Add("to turn it off: judge config set providers jev-images false");
        }

        return lines;
    }
}
