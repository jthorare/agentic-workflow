using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Desktop;

/// <summary>How a desktop-run ended; the exit code alone decides it (0 passed, 2 failed, 1 refused, 4 errored).</summary>
[JsonConverter(typeof(StrictEnumConverter<DesktopOutcome>))]
public enum DesktopOutcome
{
    /// <summary>Every screen is within its limit.</summary>
    [JsonStringEnumMemberName("passed")]
    Passed,

    /// <summary>A screen differs from its design by more than its limit, or could not be compared.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,

    /// <summary>A guard or an invalid input refused the run.</summary>
    [JsonStringEnumMemberName("refused")]
    Refused,

    /// <summary>The run itself errored (the fixture, the capture or the design browser).</summary>
    [JsonStringEnumMemberName("errored")]
    Errored,
}
