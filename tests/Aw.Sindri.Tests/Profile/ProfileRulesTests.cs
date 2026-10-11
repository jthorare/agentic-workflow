using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Profile;

public sealed class ProfileRulesTests
{
    private static readonly SindriProfile _valid = new(1, "thor",
        Services: new Dictionary<string, ServiceSettings>(StringComparer.Ordinal) { ["heartbeat"] = new("30s") });

    [Theory, Trait("Lane", "unit")]
    [InlineData("5s", 5)]
    [InlineData("90s", 90)]
    [InlineData("5m", 300)]
    [InlineData("24h", 86_400)]
    public void Cadence_parses_whole_units(string text, int seconds) =>
        Assert.Equal(TimeSpan.FromSeconds(seconds), Cadence.Parse(text).Value.Interval);

    [Theory, Trait("Lane", "unit")]
    [InlineData("4s")]
    [InlineData("25h")]
    [InlineData("0s")]
    [InlineData("30")]
    [InlineData("1.5m")]
    [InlineData("-5s")]
    [InlineData("s")]
    [InlineData("99999999999999999999s")]
    public void Cadence_refuses_anything_else(string text) =>
        Assert.Equal(SindriErrors.ProfileInvalid.Code, Cadence.Parse(text).Error.Code);

    [Fact, Trait("Lane", "unit")]
    public void A_valid_profile_comes_back_canonical_with_defaults()
    {
        var checkedProfile = ProfileRules.Check(_valid with { Repos = [new("b", "/b"), new("a", "/a")] }).Value;

        Assert.Equal("a,b", string.Join(',', checkedProfile.Repos!.Select(r => r.Name)));
        Assert.Equal(ApiSettings.DefaultPort, checkedProfile.Api!.Port);
        Assert.Equal("local", checkedProfile.Tenant);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("version", "schemaVersion: is 2; this build reads 1")]
    [InlineData("user", "user: must be non-empty text")]
    [InlineData("name", "services.Heart_Beat: is not a service name")]
    [InlineData("every", "services.heartbeat.every: 'every: 1s' is not a cadence")]
    [InlineData("port", "api.port: must be 0 (a free port) or 1024 to 65535")]
    [InlineData("privileged", "api.port: must be 0 (a free port) or 1024 to 65535")]
    public void An_invalid_value_names_its_key_path(string what, string message)
    {
        var bad = what switch
        {
            "version" => _valid with { SchemaVersion = 2 },
            "user" => _valid with { User = " " },
            "name" => _valid with { Services = new Dictionary<string, ServiceSettings>(StringComparer.Ordinal) { ["Heart_Beat"] = new("30s") } },
            "every" => _valid with { Services = new Dictionary<string, ServiceSettings>(StringComparer.Ordinal) { ["heartbeat"] = new("1s") } },
            "privileged" => _valid with { Api = new ApiSettings(Port: 80) },
            _ => _valid with { Api = new ApiSettings(Port: 70_000) },
        };

        var error = ProfileRules.Check(bad).Error;

        Assert.Equal(SindriErrors.ProfileInvalid.Code, error.Code);
        Assert.StartsWith(message, error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_diff_lists_changed_key_paths_and_everything_for_a_first_approval()
    {
        var live = ProfileRules.Check(_valid).Value;
        var changed = live with { Services = new SortedDictionary<string, ServiceSettings>(StringComparer.Ordinal) { ["heartbeat"] = new("7s") } };

        Assert.Equal(["services.heartbeat.every"], ProfileDiff.Between(live, changed));
        Assert.Empty(ProfileDiff.Between(live, live));
        Assert.Equal(["api.port", "schemaVersion", "services.heartbeat.every", "tenant", "user"], ProfileDiff.Between(approved: null, live));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(null, null, null, true)]
    [InlineData(null, null, "a", true)]
    [InlineData("a", "a", "a", false)]
    [InlineData("a", "a", "b", true)]
    [InlineData("a", "a", null, false)]
    [InlineData("a", null, "a", true)]    // saved, but its event never reached the ledger
    [InlineData("a", "b", "a", true)]     // B's event recorded, its save failed, the file edited back to A
    [InlineData("a", "b", null, true)]    // the same with a live file that doesn't read
    public void A_profile_needs_approval_unless_it_is_saved_and_the_latest_recorded_approval(string? saved, string? recorded, string? live, bool needs)
    {
        Assert.Equal(needs, ProfileRules.NeedsApproval(Hash(saved), Hash(recorded), Hash(live)));
        if (Hash(live) is { } judged)
        {
            Assert.Equal(!needs, ProfileRules.IsApproved(Hash(saved), Hash(recorded), judged));
        }
    }

    private static ProfileHash? Hash(string? which) => which is null ? null : ProfileHash.From(new string(which[0], 64));
}
