namespace Aw.Judge.Core.Images;

/// <summary>One labeled screenshot of 'judge check images': a page that looks right or has a visible break, with its PNG.</summary>
public sealed record ImageCase
{
    /// <summary>The case's name, e.g. 'broken-overlap'; the file it is written as is '&lt;name&gt;.png'.</summary>
    public required string Name { get; init; }

    /// <summary>What visual-critique should say: 'looks-right' for a page that looks right, 'looks-off' for one with a visible break.</summary>
    public required string Expected { get; init; }

    /// <summary>The PNG file's bytes.</summary>
    public required ReadOnlyMemory<byte> Bytes { get; init; }
}
