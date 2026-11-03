using System.Text.Json;
using Aw.Evidence.Core.Scripts;

namespace Aw.Evidence.Core.Visual;

/// <summary>Everything besides the pixels that could change what a cached verdict means.</summary>
/// <param name="PromptVersion">The critique prompt's version.</param>
/// <param name="Model">The critique model.</param>
/// <param name="AppBuild">The app build (commit) when known; null makes entries trusted only within their own run.</param>
/// <param name="Scenario">The hash of the executed script (<see cref="ScenarioOf"/>).</param>
/// <param name="Fixtures">The fixture or seed identity, when known.</param>
/// <param name="Viewports">The viewports the run used ('desktop,phone').</param>
/// <param name="BrowserVersion">The browser build.</param>
/// <param name="JevImages">Whether the judge sends images to Jev: a verdict formed with images on is never replayed after they are turned off, or the other way round.</param>
public sealed record VerdictCacheContext(
    string PromptVersion, string Model, string? AppBuild, string Scenario, string? Fixtures, string Viewports, string BrowserVersion, bool JevImages)
{
    /// <summary>The critique prompt version this build uses.</summary>
    public const string CurrentPromptVersion = "visual-critique/v1";

    /// <summary>The scenario hash: the script's route, role, steps and viewports (planning metadata excluded).</summary>
    /// <param name="script">The script.</param>
    /// <param name="options">The package's AwJson options.</param>
    /// <returns>The hash.</returns>
    public static string ScenarioOf(UiScript script, JsonSerializerOptions options) =>
        ContentHash.Sha256Hex(script with { Planning = null }, options);
}
