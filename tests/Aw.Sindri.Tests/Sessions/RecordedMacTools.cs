using Aw.Sindri.Infrastructure.Sessions;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>ps and lsof answering with the recordings in tests/fixtures/sessions/macos (see its README), and counting what was asked. A tool set to null answers like a missing one.</summary>
internal sealed class RecordedMacTools : IMacTools
{
    private readonly List<string> _calls = [];

    /// <summary>What ps answers, or null for a missing ps.</summary>
    public string? Ps { get; init; } = File.ReadAllText(Path.Combine(SessionFixtures.Macos, "ps.txt"));

    /// <summary>What lsof answers, by the kind of question, or null for a missing lsof.</summary>
    public bool LsofPresent { get; init; } = true;

    /// <summary>Every call as 'tool arguments'.</summary>
    public IReadOnlyList<string> Calls => _calls;

    public string? Run(string tool, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        _calls.Add(tool + " " + string.Join(' ', arguments));
        if (tool == "/bin/ps")
        {
            return Ps;
        }

        if (tool != "/usr/sbin/lsof" || !LsofPresent)
        {
            return null;
        }

        var name = arguments.Contains("-U", StringComparer.Ordinal) ? "lsof-sockets.txt" : arguments.Contains("cwd", StringComparer.Ordinal) ? "lsof-cwd.txt" : "lsof-files.txt";
        var recording = File.ReadAllText(Path.Combine(SessionFixtures.Macos, name));
        // why: lsof prints nothing for a pid that doesn't exist, so a per-process question about another pid than the recorded one gets an empty answer
        var asked = arguments.SkipWhile(static a => a != "-p").Skip(1).FirstOrDefault();
        return asked is not null && !recording.StartsWith("p" + asked + "\n", StringComparison.Ordinal) ? string.Empty : recording;
    }
}
