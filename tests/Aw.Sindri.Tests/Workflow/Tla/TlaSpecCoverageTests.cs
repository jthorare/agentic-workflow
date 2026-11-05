using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Tests.Workflow.Tla;

/// <summary>
/// A TLA+ spec of the engine (specs/tla/&lt;Name&gt;/) drifts silently when the code changes, so its README names the C# triggers it models
/// ('Actions:') and the ones it leaves out with a reason ('Leaves out:'). A new trigger or deadline kind fails here until a spec names it,
/// and a name that no longer exists fails too (model-check design §5.2). It is a name check, not a proof that the spec matches the code.
/// </summary>
public sealed class TlaSpecCoverageTests
{
    private const string KindPrefix = "DeadlineKind.";

    [Fact, Trait("Lane", "integration")]   // reads specs/tla/*/README.md off disk
    public void Some_spec_readme_lists_its_actions() => Assert.NotEmpty(Readmes());

    [Fact, Trait("Lane", "integration")]
    public void Every_engine_trigger_is_named_in_a_spec_readme()
    {
        var named = NamedInReadmes();

        var missing = Triggers().Where(name => !named.Contains(name)).Order(StringComparer.Ordinal).ToList();

        Assert.True(missing.Count == 0, $"no specs/tla/*/README.md names {string.Join(", ", missing)} under 'Actions:' or 'Leaves out:' (fix: model it, or leave it out with a reason)");
    }

    [Fact, Trait("Lane", "integration")]
    public void Every_deadline_kind_is_named_in_a_spec_readme()
    {
        var named = NamedInReadmes();

        var missing = Enum.GetNames<DeadlineKind>().Where(kind => !named.Contains(KindPrefix + kind)).Order(StringComparer.Ordinal).ToList();

        Assert.True(missing.Count == 0, $"no specs/tla/*/README.md names {string.Join(", ", missing.Select(static k => KindPrefix + k))} under 'Actions:' or 'Leaves out:'");
    }

    [Fact, Trait("Lane", "integration")]
    public void Every_name_a_spec_readme_lists_still_exists()
    {
        var types = typeof(Trigger).Assembly.GetTypes().Select(static t => t.Name).ToHashSet(StringComparer.Ordinal);
        var kinds = Enum.GetNames<DeadlineKind>().Select(static k => KindPrefix + k).ToHashSet(StringComparer.Ordinal);

        var gone = Readmes()
            .SelectMany(static r => Names(r.Text).Select(name => (r.Spec, Name: name)))
            .Where(n => n.Name.StartsWith(KindPrefix, StringComparison.Ordinal) ? !kinds.Contains(n.Name) : !types.Contains(n.Name))
            .Select(static n => $"{n.Spec}: {n.Name}")
            .ToList();

        Assert.True(gone.Count == 0, $"a README names C# that no longer exists: {string.Join("; ", gone)}");
    }

    [Fact, Trait("Lane", "integration")]
    public void A_leaves_out_line_reads_the_names_before_its_reason_and_skips_prose()
    {
        var names = Names("Actions: Takeover = TakeoverRequested · Elapse = (time passing, no C# trigger)\nLeaves out: StepFailed, WaitSatisfied: not part of takeover · Temporal replay, the ledger mirror.");

        Assert.Equal(["TakeoverRequested", "StepFailed", "WaitSatisfied"], names);
    }

    private static HashSet<string> NamedInReadmes() => Readmes().SelectMany(static r => Names(r.Text)).ToHashSet(StringComparer.Ordinal);

    /// <summary>The concrete <see cref="Trigger"/> types of the Core assembly (the 17 in Engine/Triggers/ today).</summary>
    private static IEnumerable<string> Triggers() =>
        typeof(Trigger).Assembly.GetTypes().Where(static t => t is { IsAbstract: false } && t.IsSubclassOf(typeof(Trigger))).Select(static t => t.Name);

    /// <summary>Every spec folder whose README has an 'Actions:' line (a folder without one is not an engine model).</summary>
    private static List<(string Spec, string Text)> Readmes()
    {
        var folder = Path.Combine(RepoRoot(), "specs", "tla");
        return Directory.Exists(folder)
            ? [.. Directory.EnumerateDirectories(folder).Order(StringComparer.Ordinal)
                .Select(static dir => (Spec: Path.GetFileName(dir), Path: Path.Combine(dir, "README.md")))
                .Where(static s => File.Exists(s.Path))
                .Select(static s => (s.Spec, Text: File.ReadAllText(s.Path)))
                .Where(static s => Line(s.Text, "Actions:") is not null)]
            : [];
    }

    /// <summary>
    /// The C# names a README lists. 'Actions:' is 'Action = Names · Action = (prose)'; 'Leaves out:' is 'Names: reason · prose',
    /// both with segments split on " · ". A name starts with an upper-case letter and holds letters, digits and a dot ('DeadlineKind.State').
    /// </summary>
    private static List<string> Names(string text)
    {
        var names = new List<string>();
        foreach (var segment in Segments(Line(text, "Actions:")))
        {
            var rhs = segment[(segment.IndexOf('=', StringComparison.Ordinal) + 1)..].Trim();
            if (!rhs.StartsWith('('))
            {
                names.AddRange(rhs.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            }
        }

        foreach (var segment in Segments(Line(text, "Leaves out:")))
        {
            var colon = segment.IndexOf(": ", StringComparison.Ordinal);
            var head = colon < 0 ? [] : segment[..colon].Split(", ", StringSplitOptions.TrimEntries);
            if (head.Length > 0 && head.All(IsName))
            {
                names.AddRange(head);
            }
        }

        return names;
    }

    private static string[] Segments(string? line) => line?.Split(" · ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];

    private static bool IsName(string word) => word.Length > 0 && char.IsUpper(word[0]) && word.All(static c => char.IsLetterOrDigit(c) || c == '.');

    /// <summary>The text after <paramref name="label"/> on the README line that starts with it; null when there is none.</summary>
    private static string? Line(string text, string label) =>
        text.Split('\n').Select(static l => l.TrimEnd('\r')).FirstOrDefault(l => l.StartsWith(label, StringComparison.Ordinal)) is { } line ? line[label.Length..].Trim() : null;

    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AgenticWorkflow.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("No AgenticWorkflow.slnx above the test's base directory.");
    }
}
