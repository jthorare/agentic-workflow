using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.ValidateProfile;

/// <summary>A valid profile's hash and where it was read from.</summary>
/// <param name="Hash">Its hash (what --if-hash checks).</param>
/// <param name="Source">The file.</param>
public sealed record ValidateProfileResponse(ProfileHash Hash, string Source)
{
    /// <summary>What each 'workflow.agent' pin and cap matches, one line each ('pin claude-code/questions = haiku: matches workflow.scoping state questions'); null, and left out of the JSON, when the profile has none.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Matches { get; init; }

    /// <summary>What a person should know before saving ('workflow.agent.target ... parks with SND-LAUNCH-013'); null, and left out of the JSON, when there is nothing.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Warnings { get; init; }
}
