using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Launch;

/// <summary>SL8: an unattended Step runs only approved argv (launcher spec SL8, L-11).</summary>
public sealed class StepApprovalTests
{
    private const string BuiltinHash = "builtin-hash";

    private static AgentId Id(string value) => AgentId.From(value);

    private static AgentEntry Entry(string id, CatalogSource source, string? hash) =>
        new(Id(id), Label: id, Bin: id, ProcessName: null, Transcripts: null, Resume: null) { Source = source, ArgvHash = hash };

    private static IReadOnlyList<string> Hashes(string id, AgentSettings settings) => settings.StepArgvHashes(Id(id));

    private static AgentSettings Approved(string id, params string[] hashes) =>
        new(Targets: new Dictionary<string, AgentLimits>(StringComparer.Ordinal) { [id] = new(StepArgvHash: hashes) });

    [Fact, Trait("Lane", "unit")]
    public void Step_on_a_builtin_passes()
    {
        var entry = Entry("claude-code", CatalogSource.Builtin, BuiltinHash);

        var result = StepApproval.Check(entry, BuiltinHash, approvedHashes: []);

        Assert.True(result.IsSuccess);
        Assert.Same(entry, result.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void Step_on_a_bin_only_override_passes()
    {
        var entry = Entry("claude-code", CatalogSource.Override, BuiltinHash);

        Assert.True(StepApproval.Check(entry, BuiltinHash, approvedHashes: []).IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public void Step_on_an_added_entry_without_hash_is_012_not_approved()
    {
        var entry = Entry("aider", CatalogSource.Added, "aider-hash");

        var error = StepApproval.Check(entry, builtinArgvHash: null, approvedHashes: []).Error;

        Assert.Equal(SindriErrors.LaunchNotApproved.Code, error.Code);
        Assert.Contains("not-approved", error.Message, StringComparison.Ordinal);
        Assert.Contains("sindri launch show aider", error.Message, StringComparison.Ordinal);
        Assert.Equal("not-approved", Detail(error, "reason"));
        Assert.Equal("aider-hash", Detail(error, "current"));
    }

    [Fact, Trait("Lane", "unit")]
    public void Step_after_template_change_is_012_hash_changed_with_both_hashes()
    {
        var entry = Entry("aider", CatalogSource.Added, "new-hash");

        var error = StepApproval.Check(entry, builtinArgvHash: null, Hashes("aider", Approved("aider", "old-hash"))).Error;

        Assert.Equal(SindriErrors.LaunchNotApproved.Code, error.Code);
        Assert.Equal("hash-changed", Detail(error, "reason"));
        Assert.Equal("new-hash", Detail(error, "current"));
        Assert.Equal(["old-hash"], error.Extensions!.Single(m => string.Equals(m.Key, "approved", StringComparison.Ordinal)).Value.EnumerateArray().Select(e => e.GetString()).ToList());
    }

    [Fact, Trait("Lane", "unit")]
    public void An_override_that_changed_the_template_needs_its_own_approval()
    {
        var entry = Entry("claude-code", CatalogSource.Override, "other-hash");

        Assert.Equal("not-approved", Detail(StepApproval.Check(entry, BuiltinHash, approvedHashes: []).Error, "reason"));
        Assert.True(StepApproval.Check(entry, BuiltinHash, Hashes("claude-code", Approved("claude-code", "other-hash"))).IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public void StepArgvHash_list_accepts_any_listed()
    {
        var entry = Entry("aider", CatalogSource.Added, "h2");

        Assert.True(StepApproval.Check(entry, builtinArgvHash: null, Hashes("aider", Approved("aider", "h1", "h2", "h3"))).IsSuccess);
        Assert.False(StepApproval.Check(entry, builtinArgvHash: null, Hashes("aider", Approved("aider", "h1", "h3"))).IsSuccess);
        Assert.False(StepApproval.Check(entry, builtinArgvHash: null, Hashes("aider", Approved("codex", "h2"))).IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public void Check_and_build_use_the_same_loaded_entry()
    {
        var first = Entry("aider", CatalogSource.Added, "h1") with { Open = ["{bin}", "first"] };
        var second = Entry("aider", CatalogSource.Added, "h2") with { Open = ["{bin}", "second"] };
        var catalog = new FlippingCatalog(first, second);

        var result = StepApproval.Resolve(catalog, Id("aider"), builtinArgvHash: null, Hashes("aider", Approved("aider", "h1")));

        Assert.Same(first, result.Value);
        Assert.Equal(1, catalog.Resolves);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_id_the_catalog_lacks_is_003_not_in_catalog()
    {
        var error = StepApproval.Resolve(new FlippingCatalog(), Id("ghost"), builtinArgvHash: null, approvedHashes: []).Error;

        Assert.Equal(SindriErrors.LaunchAgentMissing.Code, error.Code);
        Assert.Contains("not-in-catalog", error.Message, StringComparison.Ordinal);
    }

    private static string? Detail(Error error, string name) => error.Extensions!.Single(m => string.Equals(m.Key, name, StringComparison.Ordinal)).Value.GetString();

    private sealed class FlippingCatalog(params AgentEntry[] entries) : IAgentCatalog
    {
        public int Resolves { get; private set; }

        public IReadOnlyList<AgentEntry> All => entries;

        public AgentEntry? Resolve(AgentId id) => entries.ElementAtOrDefault(Resolves++);
    }
}
