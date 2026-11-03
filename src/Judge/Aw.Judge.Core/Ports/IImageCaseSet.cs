using Aw.Judge.Core.Images;

namespace Aw.Judge.Core.Ports;

/// <summary>The labeled screenshots 'judge check images' scores Jev and the agent CLIs on (the real set is embedded in the binary).</summary>
public interface IImageCaseSet
{
    /// <summary>The cases, in the order they are asked.</summary>
    public IReadOnlyList<ImageCase> Cases { get; }
}
