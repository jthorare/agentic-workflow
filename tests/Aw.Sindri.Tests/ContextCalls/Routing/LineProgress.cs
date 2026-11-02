namespace Aw.Sindri.Tests.ContextCalls.Routing;

/// <summary>The progress lines a job was given.</summary>
internal sealed class LineProgress : IProgress<string>
{
    public List<string> Lines { get; } = [];

    public void Report(string value)
    {
        lock (Lines)
        {
            Lines.Add(value);
        }
    }
}
