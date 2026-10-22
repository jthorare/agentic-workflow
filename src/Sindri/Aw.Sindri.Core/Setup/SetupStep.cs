using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Setup;

/// <summary>One step of the setup list, as GET /sindri/setup answers it. Every text is plain words.</summary>
/// <param name="Id">Which step.</param>
/// <param name="Title">What the step is, in a few words.</param>
/// <param name="State">Where it stands.</param>
/// <param name="Why">One sentence on why the step is in this state.</param>
/// <param name="Detail">What it did or is doing, or null.</param>
/// <param name="Error">Why it failed, or null.</param>
/// <param name="Action">The catalog action that moves it forward, or null when there is none (it runs on its own, or it waits on another step).</param>
/// <param name="Progress">How far a counted step is, or null.</param>
public sealed record SetupStep(SetupStepId Id, string Title, SetupState State, string Why, string? Detail, SetupError? Error, string? Action, SetupProgress? Progress)
{
    /// <summary>One line per repo with its own state, for a step that counts repos; empty otherwise.</summary>
    public IReadOnlyList<SetupItem> Items { get; init; } = [];

    /// <summary>What the profile allows in plain words (the profile step only); null, and left out of the JSON, for every other step and when there is no profile to read.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<SetupPreviewLine>? Preview { get; init; }
}
