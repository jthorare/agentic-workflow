namespace Aw.Forge.Infrastructure.World;

/// <summary>Everything the file/fake forge knows. Saved as one JSON document by the file adapter.</summary>
internal sealed class WorldState
{
    public List<WorldRepo> Repos { get; set; } = [];

    public List<WorldPull> Pulls { get; set; } = [];

    /// <summary>Branch tips by "remote#branch", for the fake's simulated pushes and for the file adapter's record of what it pushed.</summary>
    public Dictionary<string, string> Tips { get; set; } = [];
}
