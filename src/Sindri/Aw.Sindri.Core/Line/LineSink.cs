namespace Aw.Sindri.Core.Line;

/// <summary>What one rule reports in one run: its findings, and the file names it needed but did not find.</summary>
/// <param name="rule">The rule id the findings carry.</param>
/// <param name="facts">The files of the run.</param>
public sealed class LineSink(string rule, LineFacts facts)
{
    private readonly List<LineFinding> _findings = [];
    private readonly List<string> _missing = [];

    /// <summary>The findings so far, failures and warnings.</summary>
    public IReadOnlyList<LineFinding> Findings => _findings;

    /// <summary>The needed names the facts lack, or empty when the rule could run.</summary>
    public IReadOnlyList<string> Missing => _missing;

    /// <summary>Whether every named file is present and parses; an absent one is recorded as missing (the rule is not run), an unparseable one as a failing finding.</summary>
    /// <param name="names">The file names.</param>
    /// <returns>True when the rule can read all of them.</returns>
    public bool Need(params string[] names)
    {
        ArgumentNullException.ThrowIfNull(names);
        _missing.AddRange(facts.Missing(names));
        var readable = _missing.Count == 0;
        foreach (var name in names)
        {
            if (facts.Unparseable(name) is { } reason)
            {
                Fail(name, $"{name} is not valid: {reason}", $"rewrite {name} as valid JSON");
                readable = false;
            }
        }

        return readable;
    }

    /// <summary>A failing finding.</summary>
    /// <param name="item">What it is about.</param>
    /// <param name="message">What is wrong.</param>
    /// <param name="fix">What to change.</param>
    public void Fail(string? item, string message, string fix) => _findings.Add(new LineFinding(rule, item, message, fix, LineSeverity.Fail));

    /// <summary>A warning: shown, never changes an outcome.</summary>
    /// <param name="item">What it is about.</param>
    /// <param name="message">What is wrong.</param>
    /// <param name="fix">What to change.</param>
    public void Warn(string? item, string message, string fix) => _findings.Add(new LineFinding(rule, item, message, fix, LineSeverity.Warn));
}
