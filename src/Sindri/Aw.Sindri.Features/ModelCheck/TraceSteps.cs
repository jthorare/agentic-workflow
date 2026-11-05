using System.Text;
using System.Text.RegularExpressions;

namespace Aw.Sindri.Features.ModelCheck;

/// <summary>
/// A TLC counterexample as the owner reads it (model-check design §4.3): one numbered line per state, the action's name and the variables that changed, in the README's words when it has them; then the
/// scrubbed trace. A long trace keeps its first 5 states and last 20, never only the tail: a counterexample is read from its start.
/// </summary>
internal static class TraceSteps
{
    private const int Head = 5;
    private const int Tail = 20;

    private static readonly Regex _state = new(@"^State \d+: (?:<(?<action>[^ >]+)[^>]*>|(?<action>.*))$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
    private static readonly Regex _back = new(@"^Back to state (?<n>\d+): (?:<(?<action>[^ >]+)[^>]*>|(?<action>.*))$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
    private static readonly Regex _assign = new(@"^/\\ (?<name>\w+) = (?<value>.*)$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));

    /// <summary>The steps and the trace behind them, as the finding's detail; empty when TLC printed no states.</summary>
    /// <param name="trace">TLC's output lines, already without the runner's margin.</param>
    /// <param name="vars">Variable name to its words, from the README.</param>
    /// <returns>The detail text.</returns>
    internal static string Detail(IReadOnlyList<string> trace, IReadOnlyDictionary<string, string> vars)
    {
        ArgumentNullException.ThrowIfNull(trace);
        ArgumentNullException.ThrowIfNull(vars);
        var (states, loop) = States(trace);
        if (states.Count == 0)
        {
            return string.Join('\n', trace);
        }

        var kept = states.Count <= Head + Tail ? states : [.. states.Take(Head), .. states.Skip(states.Count - Tail)];
        var text = new StringBuilder();
        Steps(text, new Shown(states, kept, loop), vars);
        _ = text.AppendLine().AppendLine("Full trace:");
        foreach (var state in kept)
        {
            _ = text.AppendLine(state.Header);
            foreach (var line in state.Lines)
            {
                _ = text.AppendLine(line);
            }
        }

        if (loop is not null)
        {
            _ = text.AppendLine(loop.Header);
        }

        return text.ToString().TrimEnd();
    }

    private static void Steps(StringBuilder text, Shown shown, IReadOnlyDictionary<string, string> vars)
    {
        var (states, kept, loop) = shown;
        for (var i = 0; i < kept.Count; i++)
        {
            if (states.Count > Head + Tail && i == Head)
            {
                _ = text.Append("   ... ").Append(states.Count - Head - Tail).AppendLine(" states left out ...");
            }

            var previous = kept[i].Index == 0 ? null : states[kept[i].Index - 1];
            _ = text.Append(kept[i].Index + 1).Append(". ").AppendLine(Describe(kept[i], previous, vars));
        }

        if (loop is not null)
        {
            _ = text.Append(states.Count + 1).Append(". Then repeats from step ").Append(loop.Step).Append(" (").Append(loop.Action).AppendLine(")");
        }
    }

    private static string Describe(TraceState state, TraceState? previous, IReadOnlyDictionary<string, string> vars)
    {
        var action = state.Action.StartsWith("Initial", StringComparison.Ordinal) ? "Start" : state.Action;
        var changes = state.Values.Where(v => previous is null || !previous.Values.TryGetValue(v.Key, out var old) || !string.Equals(old, v.Value, StringComparison.Ordinal))
            .Select(v => previous is not null && previous.Values.TryGetValue(v.Key, out var was) ? $"{Label(v.Key, vars)} {was} -> {v.Value}" : $"{Label(v.Key, vars)} = {v.Value}");
        var said = string.Join(", ", changes);
        return $"{action}: {(said.Length == 0 ? "no change" : said)}";
    }

    private static string Label(string name, IReadOnlyDictionary<string, string> vars)
    {
        if (!vars.TryGetValue(name, out var words))
        {
            return name;
        }

        var paren = words.IndexOf('(', StringComparison.Ordinal);
        return (paren > 0 ? words[..paren] : words).Trim();
    }

    private static (List<TraceState> States, LoopBack? Loop) States(IReadOnlyList<string> trace)
    {
        var reader = new Reader();
        foreach (var line in trace)
        {
            reader.Take(line);
        }

        return (reader.States, reader.Loop);
    }

    // Reads TLC's lines one at a time: a state header opens a state, its '/\ var = value' lines fill it, a blank line closes it.
    private sealed class Reader
    {
        private bool _closed;

        internal List<TraceState> States { get; } = [];

        internal LoopBack? Loop { get; private set; }

        internal void Take(string line)
        {
            if (States.Count > 0 && _back.Match(line) is { Success: true } back)
            {
                _closed = true;   // a liveness counterexample ends with the state it loops back to, after the last state's blank line
                Loop = new LoopBack(back.Groups["n"].Value, back.Groups["action"].Value.Trim(), line);
            }
            else if (_state.Match(line) is { Success: true } header)
            {
                _closed = false;
                States.Add(new TraceState(States.Count, line, header.Groups["action"].Value.Trim(), [], new Dictionary<string, string>(StringComparer.Ordinal)));
            }
            else if (States.Count > 0 && !_closed)
            {
                Fill(line);
            }
        }

        private void Fill(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                _closed = true;   // TLC ends a state with a blank line; the statistics after the last state are not part of it
                return;
            }

            var current = States[^1];
            current.Lines.Add(line);
            if (_assign.Match(line) is { Success: true } assign)
            {
                current.Values[assign.Groups["name"].Value] = assign.Groups["value"].Value;
            }
        }
    }

    private sealed record Shown(List<TraceState> States, List<TraceState> Kept, LoopBack? Loop);

    private sealed record LoopBack(string Step, string Action, string Header);

    private sealed record TraceState(int Index, string Header, string Action, List<string> Lines, Dictionary<string, string> Values);
}
