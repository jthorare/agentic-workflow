namespace Aw.Forge.Infrastructure.World;

/// <summary>A repository in the file/fake forge.</summary>
internal sealed class WorldRepo
{
    public string Key { get; set; } = string.Empty;

    public string DefaultBranch { get; set; } = "main";

    public int NextNumber { get; set; } = 1;
}
