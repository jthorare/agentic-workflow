namespace Aw.Judge.Host.Commands;

/// <summary>
/// The global '--providers a,b' flag, accepted anywhere in argv so it composes with every command line. The table
/// takes it out before routing; only a question reads its value (else AW_JUDGE_PROVIDERS), so a list the judge can't
/// read stops questions and never health or the brief commands.
/// </summary>
internal static class ProvidersFlag
{
    private const string Flag = "--providers";

    /// <summary>argv without the flag and its value, and the value (null when the flag is absent).</summary>
    /// <param name="argv">The command line.</param>
    /// <returns>The rest and the value; a null rest when the flag has no value.</returns>
    internal static (string[]? Arguments, string? Value) Take(string[] argv)
    {
        var at = Array.FindIndex(argv, a => string.Equals(a, Flag, StringComparison.Ordinal));
        if (at < 0)
        {
            return (argv, null);
        }

        return at + 1 >= argv.Length ? (null, null) : ([.. argv[..at], .. argv[(at + 2)..]], argv[at + 1]);
    }
}
