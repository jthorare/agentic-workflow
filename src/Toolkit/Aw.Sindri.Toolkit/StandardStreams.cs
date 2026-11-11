namespace Aw.Sindri.Toolkit;

/// <summary>The standard streams a toolkit runs over: the real process streams, or test doubles.</summary>
/// <param name="OpenInput">Opens the MCP input stream.</param>
/// <param name="OpenOutput">Opens the MCP output stream (the real stdout).</param>
/// <param name="Out">Where "--manifest" writes.</param>
/// <param name="Error">Where usage text goes, and where Console.Out is redirected while serving.</param>
internal sealed record StandardStreams(Func<Stream> OpenInput, Func<Stream> OpenOutput, TextWriter Out, TextWriter Error)
{
    /// <summary>The process's own stdin, stdout and stderr.</summary>
    public static StandardStreams Console => new(System.Console.OpenStandardInput, System.Console.OpenStandardOutput, System.Console.Out, System.Console.Error);
}
