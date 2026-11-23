using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Host.Commands;

/// <summary>'sindri restart [--wait] [--timeout D]' (first-run spec §5.1): D is a whole number with s, m or h ('90s', '30m', '2h'), or seconds alone; 30 minutes by default.</summary>
/// <param name="Wait">Wait for the open runs to end instead of refusing.</param>
/// <param name="Timeout">How long --wait waits.</param>
internal sealed record RestartArguments(bool Wait, TimeSpan Timeout)
{
    /// <summary>How long --wait waits when --timeout names nothing (decision 11).</summary>
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(30);

    /// <summary>Reads the flags after 'restart'.</summary>
    /// <param name="args">The flags.</param>
    /// <returns>The arguments, or SND-CLI-001 naming the flag it didn't take.</returns>
    internal static Result<RestartArguments> Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var wait = false;
        var timeout = DefaultTimeout;
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--wait":
                    wait = true;
                    break;
                case "--timeout" when i + 1 < args.Count && Duration(args[i + 1]) is { } d:
                    timeout = d;
                    i++;
                    break;
                default:
                    return SindriErrors.UnknownVerb.With($"sindri restart doesn't take '{args[i]}' (usage: sindri restart [--wait] [--timeout 30m])");
            }
        }

        return new RestartArguments(wait, timeout);
    }

    private static TimeSpan? Duration(string text)
    {
        var unit = text.Length > 0 && char.IsAsciiLetter(text[^1]) ? text[^1] : 's';
        var digits = text.Length > 0 && char.IsAsciiLetter(text[^1]) ? text[..^1] : text;
        if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n <= 0)
        {
            return null;
        }

        return unit switch
        {
            's' => TimeSpan.FromSeconds(n),
            'm' => TimeSpan.FromMinutes(n),
            'h' => TimeSpan.FromHours(n),
            _ => null,
        };
    }
}
