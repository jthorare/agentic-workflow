namespace Aw.Forge.Infrastructure.World;

/// <summary>One seeded check run in the file/fake forge.</summary>
internal sealed class WorldRun
{
    public string Name { get; set; } = string.Empty;

    public string? App { get; set; }

    public string Conclusion { get; set; } = string.Empty;

    public string Sha { get; set; } = string.Empty;
}
