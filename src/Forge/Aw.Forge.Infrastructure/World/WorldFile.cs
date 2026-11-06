using Aw.Forge.Core.Pulls;

namespace Aw.Forge.Infrastructure.World;

/// <summary>One changed file of a pull request in the file/fake forge, with the texts FileText reads.</summary>
internal sealed class WorldFile
{
    public string Path { get; set; } = string.Empty;

    public string? PreviousPath { get; set; }

    public PullFileStatus Status { get; set; } = PullFileStatus.Modified;

    public int Additions { get; set; }

    public int Deletions { get; set; }

    public string? Patch { get; set; }

    public string? Text { get; set; }

    public string? BaseText { get; set; }
}
