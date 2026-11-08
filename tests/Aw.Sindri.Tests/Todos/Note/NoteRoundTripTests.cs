using System.Globalization;
using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Todos.Note;
using static Aw.Sindri.Tests.Todos.Note.NoteSamples;

namespace Aw.Sindri.Tests.Todos.Note;

/// <summary>The round-trip property of todo spec §4.5 and TP-9: render a valid todo, parse the line, get the same todo back. No property-test package is in the repo, so a generator over seeded <see cref="Random"/> draws the cases: five fixed seeds of 120 todos each, so a failure names a seed and an index and reruns the same.</summary>
[Lane(TestLane.Unit)]
public sealed class NoteRoundTripTests
{
    private static readonly string[] _words = ["reply", "to", "Sam", "ship", "the", "fix", "ünï", "日本", "a-b", "x_y", "100%", "#tag", "\"quoted\"", "e.g."];

    /// <summary>Every sequence <see cref="NoteRenderer.Escape"/> escapes, and the backslash that could be mistaken for one of its escapes.</summary>
    private static readonly string[] _tricky = ["`", "<", "![[", "[[", "%%", "$=", "\\", "\\<", "\\`", "![", "[", "[[[", "%%%", "$", "\\[[", "$==", "%%%%", "``"];

    private static readonly string[] _links =
    [
        "sindri://reviews/jthorare/agentic-workflow/12",
        "sindri://linear/ENG-12",
        "sindri://item/ITM-0042",
    ];

    public static TheoryData<int> Seeds => [1, 2, 3, 4, 5];

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Render_then_parse_gives_the_same_todo(int seed)
    {
        var random = new Random(seed);
        for (var i = 0; i < 120; i++)
        {
            var todo = Generate(random, i + 1);

            var parsed = NoteParser.Parse(NoteRenderer.RenderLine(todo) + "\n").Todos.Single();

            var where = string.Format(CultureInfo.InvariantCulture, "seed {0}, case {1}: {2}", seed, i, NoteRenderer.RenderLine(todo));
            Assert.True(parsed.Break == NoteBreak.None, where);
            Assert.True(parsed.Id == todo.Id, where);
            Assert.True(string.Equals(parsed.Text, todo.Text, StringComparison.Ordinal), where + $" => text '{parsed.Text}'");
            Assert.True(parsed.Due == todo.Due, where);
            Assert.True(parsed.Done == todo.Done, where);
            Assert.True(parsed.DoneOn == todo.DoneOn, where);
            Assert.True(parsed.Links.SequenceEqual(todo.Links, StringComparer.Ordinal), where);
        }
    }

    [Fact]
    public void The_generator_reaches_every_escaped_sequence()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var random = new Random(1);
        for (var i = 0; i < 600; i++)
        {
            var text = Generate(random, 1).Text;
            foreach (var sequence in new[] { "`", "<", "![[", "[[", "%%", "$=" })
            {
                if (text.Contains(sequence, StringComparison.Ordinal))
                {
                    _ = seen.Add(sequence);
                }
            }
        }

        Assert.Equal(6, seen.Count);
    }

    private static SindriTodo Generate(Random random, int n)
    {
        var text = new StringBuilder();
        var parts = random.Next(1, 8);
        for (var p = 0; p < parts; p++)
        {
            if (p > 0)
            {
                _ = text.Append(' ');
            }

            _ = random.Next(3) == 0
                ? text.Append(_words[random.Next(_words.Length)]).Append(_tricky[random.Next(_tricky.Length)]).Append(_words[random.Next(_words.Length)])
                : text.Append(_words[random.Next(_words.Length)]);
        }

        var edges = random.Next(4);
        var run = _tricky[random.Next(_tricky.Length)] + _tricky[random.Next(_tricky.Length)] + _tricky[random.Next(_tricky.Length)];
        var body = text.ToString();
        body = edges switch
        {
            1 => _tricky[random.Next(_tricky.Length)] + body,
            2 => body + _tricky[random.Next(_tricky.Length)],
            3 => run + body + run,
            _ => body,
        };

        var done = random.Next(2) == 0;
        var links = Enumerable.Range(0, random.Next(0, 4)).Select(_ => _links[random.Next(_links.Length)]).Distinct(StringComparer.Ordinal).ToList();
        return new SindriTodo(
            Id(n),
            body,
            random.Next(2) == 0 ? D("2026-11-01").AddDays(random.Next(0, 400)) : null,
            links,
            done,
            done && random.Next(2) == 0 ? D("2026-11-01").AddDays(random.Next(0, 400)) : null);
    }
}
