using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Desktop;

/// <summary>One screen: the script that opens it in the app, which mockup is its design, the data it needs and what may differ.</summary>
/// <param name="Name">Lowercase letters, digits and '-'; names the screen's files.</param>
/// <param name="Steps">The script: a launch (implied when absent), the clicks, typing and waits that bring the screen up, and exactly one screenshot, which is the frame compared.</param>
/// <param name="Design">The mockup's HTML file, relative to the design root.</param>
/// <param name="Fixture">The fixture steps this screen needs beyond the config's 'up'; each is the arguments of one step.</param>
/// <param name="Threshold">This screen's limit of differing pixels, in percent; the manifest's when null.</param>
/// <param name="Masks">Regions painted over in both frames before they are compared (live data, clocks, ids).</param>
/// <param name="Accepted">Differences the owner accepted, in words, for the review step to skip.</param>
/// <param name="Sizes">The sizes this screen is captured at, 'WxH'; the config's when null.</param>
/// <param name="SettleMs">How long the design page may settle before its screenshot; default 1000.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DesktopScreen(
    string Name,
    IReadOnlyList<DesktopStep> Steps,
    string Design,
    IReadOnlyList<IReadOnlyList<string>>? Fixture = null,
    double? Threshold = null,
    IReadOnlyList<ScreenMask>? Masks = null,
    IReadOnlyList<string>? Accepted = null,
    IReadOnlyList<string>? Sizes = null,
    int? SettleMs = null)
{
    /// <summary>How long the design page settles when the screen says nothing.</summary>
    public const int DefaultSettleMs = 1000;
}
