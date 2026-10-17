using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Tests.Sessions;

namespace Aw.Sindri.Tests.Launch.Config;

/// <summary>The argv hash (launcher spec section 2) against SL-0's goldens; the goldens are read, never copied here, so the L-17 regeneration only re-runs this.</summary>
public sealed class ArgvHashGoldenTests
{
    private static readonly string _directory = Path.Combine(SessionFixtures.Launcher, "argv-hash");

    public static TheoryData<string> Goldens
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var file in Directory.EnumerateFiles(_directory, "*.json").Order(StringComparer.Ordinal))
            {
                data.Add(Path.GetFileName(file));
            }

            return data;
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void The_goldens_directory_is_not_empty() =>
        Assert.True(Directory.EnumerateFiles(_directory, "*.json").Skip(10).Any());

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Goldens))]
    public void ArgvHash_equals_every_argv_hash_golden(string file)
    {
        var golden = JsonNode.Parse(File.ReadAllText(Path.Combine(_directory, file)))!.AsObject();
        var (id, user) = golden.ContainsKey("entry") ? ("golden-entry", Doc("golden-entry", golden["entry"]!)) : golden.ContainsKey("override") ? (golden["builtin"]!.GetValue<string>(), Doc(golden["builtin"]!.GetValue<string>(), golden["override"]!)) : (golden["builtin"]!.GetValue<string>(), null);

        var config = LauncherConfigFile.Read(SessionFixtures.Builtins(), user).Value;
        var entry = LauncherText.Entry(config, id);

        Assert.Equal(golden["argvHash"]!.GetValue<string>(), entry.ArgvHash);
        if (golden["equalsBuiltin"] is { } same)
        {
            Assert.Equal(LauncherText.Entry(LauncherConfigFile.Read(SessionFixtures.Builtins(), user: null).Value, same.GetValue<string>()).ArgvHash, entry.ArgvHash);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void Bin_directory_only_override_hashes_equal_to_its_builtin()
    {
        var config = LauncherText.Strict("""{ "schema": 1, "agents": { "claude-code": { "override": true, "bin": "/opt/claude/bin/claude" } } }""");
        var plain = LauncherConfigFile.Read(SessionFixtures.Builtins(), user: null).Value;

        Assert.Equal(LauncherText.Entry(plain, "claude-code").ArgvHash, LauncherText.Entry(config, "claude-code").ArgvHash);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_open_template_change_or_a_form_change_changes_the_hash_and_a_label_change_does_not()
    {
        var plain = LauncherText.Entry(LauncherConfigFile.Read(SessionFixtures.Builtins(), user: null).Value, "codex").ArgvHash;

        static string? Hash(string field) => LauncherText.Entry(LauncherText.Strict($$"""{ "schema": 1, "agents": { "codex": { "override": true, {{field}} } } }"""), "codex").ArgvHash;

        Differs(plain, Hash("\"open\": [\"{bin}\", \"{seed}\"]"));
        Differs(plain, Hash("\"model\": \"none\""));
        Assert.Equal(plain, Hash("\"label\": \"Codex CLI\""));
        Assert.Equal(plain, Hash("\"model\": \"codex-flag\""));
    }

    private static void Differs(string? expected, string? actual) => Assert.False(string.Equals(expected, actual, StringComparison.Ordinal));

    private static byte[] Doc(string id, JsonNode entry) =>
        JsonSerializer.SerializeToUtf8Bytes(new JsonObject { ["schema"] = 1, ["agents"] = new JsonObject { [id] = entry.DeepClone() } });
}
