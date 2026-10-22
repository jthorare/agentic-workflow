using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Aw.Sindri.Core.Profile;

/// <summary>How a profile's command line becomes the program and arguments that run (line spec LN-WF9, decision A): no shell.</summary>
public static class CommandLine
{
    /// <summary>
    /// Splits <paramref name="line"/> on spaces and tabs; a "double-quoted" group is one argument (it may sit inside a word, as in <c language="text">--filter="a b"</c>, and <c language="text">""</c> is an empty
    /// argument). There is no escape and no expansion: a variable, a pipe or a glob stays text.
    /// </summary>
    /// <param name="line">The command line.</param>
    /// <param name="argv">The program, then its arguments; empty when the line is not valid.</param>
    /// <param name="problem">Why the line is not valid; null when it is.</param>
    /// <returns>True when the line names a program and every quote is closed.</returns>
    public static bool TryParse(string line, out IReadOnlyList<string> argv, [NotNullWhen(false)] out string? problem)
    {
        ArgumentNullException.ThrowIfNull(line);
        var (args, closed) = Split(line);
        if (!closed)
        {
            argv = [];
            problem = "has a double quote that is never closed";
            return false;
        }

        if (args.Count == 0 || args[0].Length == 0)
        {
            argv = [];
            problem = "names no program";
            return false;
        }

        argv = args;
        problem = null;
        return true;
    }

    private static (List<string> Args, bool Closed) Split(string line)
    {
        var args = new List<string>();
        var word = new StringBuilder();
        var inWord = false;
        var quoted = false;
        foreach (var c in line)
        {
            if (c == '"')
            {
                quoted = !quoted;
                inWord = true;
            }
            else if (!quoted && c is ' ' or '\t')
            {
                if (inWord)
                {
                    args.Add(word.ToString());
                    _ = word.Clear();
                    inWord = false;
                }
            }
            else
            {
                _ = word.Append(c);
                inWord = true;
            }
        }

        if (inWord)
        {
            args.Add(word.ToString());
        }

        return (args, !quoted);
    }
}
