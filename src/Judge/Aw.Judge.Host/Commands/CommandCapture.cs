using System.Text;

namespace Aw.Judge.Host.Commands;

/// <summary>A test's stand-in for the process's streams (<see cref="CommandOutput.Capture"/>).</summary>
/// <param name="Stdin">What stdin holds.</param>
internal sealed record CommandCapture(string Stdin)
{
    /// <summary>What was written to stdout.</summary>
    internal StringBuilder Stdout { get; } = new();

    /// <summary>What was written to stderr.</summary>
    internal StringBuilder Stderr { get; } = new();
}
