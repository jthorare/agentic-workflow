namespace Aw.Sindri.Core.Onyx;

/// <summary>Where `sindri onyx up` reports what it is doing while it works: lines and dots on standard error (standard output carries only the summary).</summary>
public interface IOnyxProgress
{
    /// <summary>Reports one line, such as a step starting or a warning.</summary>
    /// <param name="text">The line, without a newline.</param>
    public void Line(string text);

    /// <summary>Reports that a long wait is still going (a dot).</summary>
    public void Tick();
}
