using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Launch;

/// <summary>What a terminal viewer did (launcher L-10): the 'outcome' of 'launch.started''s viewer.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ViewerOutcomeKind>))]
public enum ViewerOutcomeKind
{
    /// <summary>A client attached inside the observation window.</summary>
    [JsonStringEnumMemberName("opened")]
    Opened,

    /// <summary>The viewer ran without error but no client attached yet; 'launch.viewer-attached' may follow.</summary>
    [JsonStringEnumMemberName("unconfirmed")]
    Unconfirmed,

    /// <summary>No viewer ran: an unattended launch, or the 'print' viewer.</summary>
    [JsonStringEnumMemberName("skipped")]
    Skipped,

    /// <summary>The viewer failed (SND-LAUNCH-001, or -009 after the host started); the session keeps running.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,
}
