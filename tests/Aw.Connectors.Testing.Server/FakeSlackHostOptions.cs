using System.Globalization;
using System.Net;
using Aw.Connectors.Testing.Slack;

namespace Aw.Connectors.Testing.Server;

/// <summary>
/// The host's command line (messages spec §8.1: the fixture starts "with seeded DMs, threads, a gap and a 429 storm"):
/// [--port N] [--empty] [--rate-limit &lt;method&gt;=&lt;times&gt;[:&lt;retry-after seconds&gt;]]... [--gap &lt;conversation&gt;=&lt;count&gt;]...
/// A rate limit queues that many 429s for one method at start (Retry-After 30 s unless given), through <see cref="FakeSlackServer.ScriptRateLimit(string, int?, int)"/>.
/// A gap adds that many older messages to one seeded conversation (<see cref="SlackWorkspaceSeed.AddOlderMessages"/>), so a reader that pages back into a
/// history 429 ends with a hole.
/// </summary>
public sealed class FakeSlackHostOptions
{
    /// <summary>The usage line the host prints on a bad command line.</summary>
    public const string Usage = "usage: Aw.Connectors.Testing.Server [--port N] [--empty] [--rate-limit <method>=<times>[:<seconds>]]... [--gap <conversation>=<count>]...";

    private const int DefaultRetryAfter = 30;

    private readonly List<(string Method, int Times, int RetryAfter)> _rateLimits = [];
    private readonly List<(string Conversation, int Count)> _gaps = [];

    /// <summary>The loopback port; 0 picks a free one.</summary>
    public int Port { get; private set; }

    /// <summary>Whether SlackWorkspaceSeed is seeded (false with --empty).</summary>
    public bool Seed { get; private set; } = true;

    /// <summary>The 429s to queue at start: method, how many, Retry-After seconds.</summary>
    public IReadOnlyList<(string Method, int Times, int RetryAfter)> RateLimits => _rateLimits;

    /// <summary>The older messages to add at start: conversation and how many.</summary>
    public IReadOnlyList<(string Conversation, int Count)> Gaps => _gaps;

    /// <summary>Reads the command line.</summary>
    /// <param name="args">The arguments.</param>
    /// <returns>The options, or the sentence that says what is wrong.</returns>
    public static (FakeSlackHostOptions? Options, string? Error) Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var options = new FakeSlackHostOptions();
        for (var i = 0; i < args.Count; i++)
        {
            var (error, used) = options.Read(args[i], i + 1 < args.Count ? args[i + 1] : null);
            if (error is not null)
            {
                return (null, error);
            }

            i += used;
        }

        return options.Gaps.Count > 0 && !options.Seed ? (null, "--gap needs the seeded workspace: drop --empty") : (options, null);
    }

    /// <summary>Seeds and scripts <paramref name="server"/> as the options say.</summary>
    /// <param name="server">The fake.</param>
    /// <returns>The fake.</returns>
    public FakeSlackServer Apply(FakeSlackServer server)
    {
        ArgumentNullException.ThrowIfNull(server);
        if (Seed)
        {
            _ = server.SeedWorkspace();
        }

        foreach (var (conversation, count) in _gaps)
        {
            _ = SlackWorkspaceSeed.AddOlderMessages(server, conversation, count);
        }

        foreach (var (method, times, retryAfter) in _rateLimits)
        {
            _ = server.ScriptRateLimit(method, retryAfter, times);
        }

        return server;
    }

    // One argument and the value after it: the sentence that says what is wrong (or null), and how many values it took.
    private (string? Error, int Used) Read(string argument, string? value) => argument switch
    {
        "--empty" => (Empty(), 0),
        "--port" => (ReadPort(value), 1),
        "--rate-limit" => (ReadRateLimit(value), 1),
        "--gap" => (ReadGap(value), 1),
        _ => ("unknown argument: " + argument, 0),
    };

    private string? Empty()
    {
        Seed = false;
        return null;
    }

    private string? ReadPort(string? value)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port > IPEndPoint.MaxPort)
        {
            return "--port needs a number from 0 to 65535";
        }

        Port = port;
        return null;
    }

    private string? ReadRateLimit(string? value)
    {
        var (method, rest) = Split(value);
        var parts = rest.Split(':');
        if (!FakeSlackServer.Serves(method) || parts.Length > 2 || Positive(parts[0]) is not { } times)
        {
            return "--rate-limit needs <method>=<times>[:<seconds>] with a method the fake serves, got " + (value ?? "nothing");
        }

        var retryAfter = parts.Length == 2 ? Positive(parts[1]) : DefaultRetryAfter;
        if (retryAfter is not { } seconds)
        {
            return "--rate-limit's Retry-After must be a positive number of seconds, got " + value;
        }

        _rateLimits.Add((method, times, seconds));
        return null;
    }

    private string? ReadGap(string? value)
    {
        var (conversation, rest) = Split(value);
        if (!SlackWorkspaceSeed.ConversationIds.Contains(conversation, StringComparer.Ordinal) || Positive(rest) is not { } count)
        {
            return "--gap needs <conversation>=<count> with a seeded conversation (" + string.Join(", ", SlackWorkspaceSeed.ConversationIds) + "), got " + (value ?? "nothing");
        }

        _gaps.Add((conversation, count));
        return null;
    }

    private static (string Name, string Tail) Split(string? value)
    {
        var at = value?.IndexOf('=', StringComparison.Ordinal) ?? -1;
        return at > 0 ? (value![..at], value[(at + 1)..]) : (string.Empty, string.Empty);
    }

    private static int? Positive(string text)
        => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > 0 ? n : null;
}
