namespace Aw.Sindri.Core.Toolkits;

/// <summary>A toolkit Sindri knows: its folder, state, the source hash of its last good build and its manifest.</summary>
/// <param name="Name">The toolkit name, also the name of its folder.</param>
/// <param name="Dir">The toolkit's folder.</param>
/// <param name="State">Where it stands.</param>
/// <param name="SourceHash">The hash of the source the last good build used; null before one.</param>
/// <param name="BuiltAt">When that build finished; null before one.</param>
/// <param name="Manifest">The manifest read after that build; null before one.</param>
/// <param name="CreatedBy">Who made the toolkit: a person or an agent session.</param>
public sealed record Toolkit(string Name, string Dir, ToolkitState State, string? SourceHash, DateTimeOffset? BuiltAt, ToolkitManifest? Manifest, string CreatedBy)
{
    /// <summary>Whether a start may go ahead for this source.</summary>
    /// <param name="currentSourceHash">The hash of the source on disk now.</param>
    /// <returns>True when a good build exists for exactly this source.</returns>
    public bool IsBuiltFor(string currentSourceHash) =>
        State is ToolkitState.Built or ToolkitState.Running or ToolkitState.Paused or ToolkitState.Failed   // failed keeps its hash and manifest only when the supervisor gave up; a failed build clears both
        && SourceHash is not null
        && Manifest is not null
        && string.Equals(SourceHash, currentSourceHash, StringComparison.Ordinal);
}
