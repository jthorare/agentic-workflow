using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Tests.Sandbox;

/// <summary>The owner-voice goldens: one sentence per (code, reason), the attended wording, the doctor lines, the status line (sandbox spec §7, §8; SB-0).</summary>
public sealed class SandboxCopyTests
{
    private static JsonArray Entries => SandboxGoldens.Object("copy.json")["entries"]!.AsArray();

    [Fact, Trait("Lane", "unit")]
    public void The_copy_covers_every_code_and_reason_exactly_once()
    {
        var keys = Entries.Select(e => ((string)e!["code"]!, (string)e["reason"]!)).Distinct().ToList();

        Assert.Equal(SandboxReasons.All.Order(), keys.Order());
    }

    [Fact, Trait("Lane", "unit")]
    public void A_017_sentence_exists_for_each_source()
    {
        foreach (var reason in new[] { "no-login", "expired" })
        {
            var sources = Entries.Where(e => (string)e!["code"]! == "SND-LAUNCH-017" && (string)e["reason"]! == reason).Select(e => (string?)e!["source"]).Order(StringComparer.Ordinal).ToList();

            Assert.Equal(["owner-copy", "separate-login"], sources);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void Owner_strings_use_the_sandbox_vocabulary()
    {
        var banned = SandboxGoldens.Object("copy.json")["bannedWords"]!.AsArray().Select(w => (string)w!).ToList();
        var strings = Entries.SelectMany(e => new[] { (string)e!["text"]!, (string)e["attendedText"]! })
            .Concat(AllStrings(SandboxGoldens.Object("states.json")))
            .Concat(AllStrings(SandboxGoldens.Object("accessibility.json")))
            .Concat(AllStrings(SandboxGoldens.Object("doctor.json")))
            .Concat([SandboxGoldens.Text("statusline.txt"), SandboxGoldens.Text("statusline-attended.txt"), SandboxGoldens.Text("takeover-banner.txt"), SandboxGoldens.Text("refusal.txt")]);

        foreach (var text in strings)
        {
            foreach (var word in banned)
            {
                Assert.False(Regex.IsMatch(text, $@"\b{word}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)), $"'{word}' in: {text}");
            }
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void The_attended_wording_never_offers_the_desk()
        => Assert.All(Entries, e => Assert.DoesNotContain("desk", (string)e!["attendedText"]!, StringComparison.OrdinalIgnoreCase));

    [Fact, Trait("Lane", "unit")]
    public void The_desk_suffix_is_for_014_and_017_only()
    {
        var root = SandboxGoldens.Object("copy.json");
        var codes = root["deskCodes"]!.AsArray().Select(c => (string)c!).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(["SND-LAUNCH-014", "SND-LAUNCH-017"], codes);
        Assert.Contains("desk", (string)root["deskSuffix"]!, StringComparison.OrdinalIgnoreCase);
        Assert.All(Entries.Where(e => codes.Contains((string)e!["code"]!)), e => Assert.DoesNotContain("desk", (string)e!["text"]!, StringComparison.OrdinalIgnoreCase));
    }

    [Fact, Trait("Lane", "unit")]
    public void Unsupported_platform_and_the_wait_are_never_red()
    {
        var states = Entries.Where(e => e!["state"] is not null).ToDictionary(e => (string)e!["reason"]!, e => (string)e!["state"]!, StringComparer.Ordinal);

        Assert.Equal(("gray", "wait"), (states["unsupported-platform"], states["sessions"]));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_status_lines_fit_in_eighty_characters_with_the_usual_prefix()
    {
        foreach (var name in new[] { "statusline.txt", "statusline-attended.txt" })
        {
            var line = SandboxGoldens.Text(name).TrimEnd('\n').Replace("<prefix>", "Ctrl-b", StringComparison.Ordinal);

            Assert.InRange(line.Length, 1, 79);
            Assert.Contains("push and secrets blocked", line, StringComparison.Ordinal);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void The_attended_status_line_says_attended() =>
        Assert.StartsWith("Sandboxed (attended):", SandboxGoldens.Text("statusline-attended.txt"), StringComparison.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void Doctor_lines_that_borrow_a_sentence_name_a_real_pair()
    {
        foreach (var line in SandboxGoldens.Object("doctor.json")["lines"]!.AsArray().Where(l => l!["copy"] is not null))
        {
            var pair = ((string)line!["copy"]!["code"]!, (string)line["copy"]!["reason"]!);

            Assert.Contains(pair, SandboxReasons.All);
        }
    }

    private static IEnumerable<string> AllStrings(JsonNode node) => node switch
    {
        JsonValue v when v.TryGetValue<string>(out var s) => [s],
        JsonObject o => o.Where(p => p.Key is not ("note" or "id" or "level")).SelectMany(p => AllStrings(p.Value!)),
        JsonArray a => a.SelectMany(i => AllStrings(i!)),
        _ => [],
    };
}
