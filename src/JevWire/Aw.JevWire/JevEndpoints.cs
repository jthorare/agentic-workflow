namespace Aw.JevWire;

/// <summary>Where Jev lives and what the judge and Sindri send it.</summary>
public static class JevEndpoints
{
    /// <summary>POST here with a 'Bearer' key: the TypeSafe API's /v1/systemone.</summary>
    public const string SystemOne = "https://api.typesafe.ai/v1/systemone";

    /// <summary>The Jev model and version both callers ask for, so a cache that keys answers by it misses on a new model.</summary>
    public const string Model = "jev-1.13.0";

    /// <summary>The most characters of state and instructions together a call may carry (Jev is a third party). Callers cut the longest strings first.</summary>
    public const int InputCapChars = 160_000;

    /// <summary>The API's choice-option limit.</summary>
    public const int MaxChoiceOptions = 255;

    /// <summary>The API's score-level limit.</summary>
    public const int MaxScoreLevels = 10;
}
