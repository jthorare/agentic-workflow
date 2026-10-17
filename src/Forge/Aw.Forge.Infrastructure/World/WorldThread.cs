using Aw.Forge.Core.Pulls;

namespace Aw.Forge.Infrastructure.World;

/// <summary>One conversation item in the file/fake forge.</summary>
internal sealed class WorldThread
{
    public string Key { get; set; } = string.Empty;

    public CommentKind Kind { get; set; } = CommentKind.Comment;

    public string Author { get; set; } = string.Empty;

    public string Association { get; set; } = "NONE";

    public bool IsBot { get; set; }

    public bool Resolved { get; set; }

    public bool Outdated { get; set; }

    public string? Path { get; set; }

    public string Body { get; set; } = string.Empty;

    public List<string> Replies { get; set; } = [];

    public List<string> Reactions { get; set; } = [];
}
