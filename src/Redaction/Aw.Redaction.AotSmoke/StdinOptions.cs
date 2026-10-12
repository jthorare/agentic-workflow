using System.Globalization;

namespace Aw.Redaction.AotSmoke;

/// <summary>The options after "--stdin": extra patterns as "--extra id=pattern" and "--timeout-ms n".</summary>
/// <param name="Extras">The extra patterns, in argument order.</param>
/// <param name="Timeout">The per-pattern match timeout (the redactor's default unless given).</param>
internal sealed record StdinOptions(IReadOnlyList<ExtraPattern> Extras, TimeSpan Timeout)
{
    /// <summary>Parses the arguments after "--stdin"; false on an unknown or incomplete argument.</summary>
    public static bool TryParse(ReadOnlySpan<string> args, out StdinOptions options)
    {
        var extras = new List<ExtraPattern>();
        var timeout = Redactor.DefaultMatchTimeout;
        options = new StdinOptions(extras, timeout);
        for (var at = 0; at < args.Length; at += 2)
        {
            if (at + 1 >= args.Length)
            {
                return false;
            }

            var value = args[at + 1];
            switch (args[at])
            {
                case "--extra" when value.IndexOf('=', StringComparison.Ordinal) is var eq and > 0:
                    extras.Add(new ExtraPattern(value[..eq], value[(eq + 1)..]));
                    break;
                case "--timeout-ms" when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var ms) && ms > 0:
                    timeout = TimeSpan.FromMilliseconds(ms);
                    break;
                default:
                    return false;
            }
        }

        options = new StdinOptions(extras, timeout);
        return true;
    }
}
