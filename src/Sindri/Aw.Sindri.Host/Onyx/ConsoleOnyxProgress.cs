using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Host.Onyx;

/// <summary>`onyx up`'s progress on standard error: a line per step, and a dot per tick (the long pull and the readiness wait), so standard output stays the summary alone.</summary>
/// <param name="error">Standard error.</param>
internal sealed class ConsoleOnyxProgress(TextWriter error) : IOnyxProgress
{
    private readonly Lock _gate = new();
    private bool _inDots;

    /// <inheritdoc />
    public void Line(string text)
    {
        lock (_gate)
        {
            if (_inDots)
            {
                error.WriteLine();
                _inDots = false;
            }

            error.WriteLine(text);
        }
    }

    /// <inheritdoc />
    public void Tick()
    {
        lock (_gate)
        {
            error.Write('.');
            error.Flush();
            _inDots = true;
        }
    }
}
