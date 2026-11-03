namespace Aw.Judge.Core.Ports;

/// <summary>A directory holding the bytes that were read, for an agent CLI to open.</summary>
public interface IImageFolder
{
    /// <summary>The directory.</summary>
    public string Path { get; }

    /// <summary>The files in it, relative to <see cref="Path"/> with '/' separators, in the order of the images written.</summary>
    public IReadOnlyList<string> Files { get; }
}
