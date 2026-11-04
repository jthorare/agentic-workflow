namespace Aw.Judge.Core.Config;

/// <summary>Whether images are sent to Jev: AW_JUDGE_JEV_IMAGES=0|1 in the environment wins over config.json's providers.jevImages, which defaults to on since the owner's decision on 2026-11-03 (q175), made knowing that day's judge check images missed its bar.</summary>
public static class JevImagesSetting
{
    /// <summary>The environment variable.</summary>
    public const string Variable = "AW_JUDGE_JEV_IMAGES";

    /// <summary>Resolves the setting.</summary>
    /// <param name="config">config.json's providers block, or null.</param>
    /// <param name="environment">The variable's value, or null when unset (anything but 0 or 1 is ignored).</param>
    /// <returns>True when images go to Jev.</returns>
    public static bool IsOn(ProvidersConfig? config, string? environment) => environment switch
    {
        "1" => true,
        "0" => false,
        _ => config?.JevImages != false,
    };
}
