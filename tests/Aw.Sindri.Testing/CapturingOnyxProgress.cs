using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Testing;

/// <summary>Progress that remembers what it was told.</summary>
public sealed class CapturingOnyxProgress : IOnyxProgress
{
    /// <summary>Gets the lines reported, in order.</summary>
    public IList<string> Lines { get; } = [];

    /// <summary>Gets or sets another progress every call is also passed to (a test points it at the console adapter to see what a user sees).</summary>
    public IOnyxProgress? Forward { get; set; }

    /// <summary>Gets the number of ticks.</summary>
    public int Ticks { get; private set; }

    /// <inheritdoc />
    public void Line(string text)
    {
        Lines.Add(text);
        Forward?.Line(text);
    }

    /// <inheritdoc />
    public void Tick()
    {
        Ticks++;
        Forward?.Tick();
    }
}
