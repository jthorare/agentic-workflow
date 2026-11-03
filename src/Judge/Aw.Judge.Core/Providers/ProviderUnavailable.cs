namespace Aw.Judge.Core.Providers;

/// <summary>The provider couldn't answer: not installed, no key, or 'timeout' (which the engine counts as a failure).</summary>
/// <param name="ReasonCode">Why, e.g. 'no-api-key' or 'timeout'.</param>
public sealed record ProviderUnavailable(string ReasonCode) : ProviderResult(ReasonCode)
{
    /// <summary>The reason code a provider returns when it ran past the question's time budget.</summary>
    public const string Timeout = "timeout";

    /// <summary>The reason code Jev returns for an image call while sending images to Jev is off (config.json's providers.jevImages, or AW_JUDGE_JEV_IMAGES=0).</summary>
    public const string ImagesOff = "images-off";

    /// <summary>The reason code Jev returns when an image is outside what it takes (280 to 1568 px a side, 78,400 to 2,458,624 pixels).</summary>
    public const string ImageOutOfLimits = "image-out-of-limits";

    /// <summary>The reason code Jev returns when it answers 413 to a call with images.</summary>
    public const string ImageTooLarge = "image-too-large";
}
