using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Skills;

namespace Aw.Sindri.Tests.Skills;

/// <summary>What <see cref="FileRuleCatalog"/> reads from real records beyond the contract: skipped records, the record folders it reads and a missing rules folder.</summary>
[Lane(TestLane.Integration)]
public sealed class FileRuleCatalogTests : IDisposable
{
    private const string Good = "schemaVersion: 1\nid: engineering.csharp.good\nkind: engineering\nsection: \"4b.1\"\ntext: \"A rule with enough text.\"\nstatus: active\n";

    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("aw-rule-catalog-");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _folder.Delete(recursive: true);

    [Fact]
    public async Task A_record_that_does_not_parse_or_has_no_id_or_text_is_skipped_and_counted()
    {
        Write("engineering/csharp/good.yaml", Good);
        Write("engineering/csharp/broken.yaml", "id: [unclosed\ntext: \"x\n");
        Write("engineering/csharp/no-id.yaml", "kind: engineering\ntext: \"A rule without an id.\"\n");
        Write("engineering/csharp/no-text.yaml", "id: engineering.csharp.no-text\nkind: engineering\n");
        Write("engineering/csharp/list.yaml", "- one\n- two\n");

        var listed = (await new FileRuleCatalog(_folder.FullName).List(Token)).Value;

        Assert.Equal(["engineering.csharp.good"], listed.Rules.Select(static r => r.Id));
        Assert.Equal(4, listed.Skipped);
    }

    [Fact]
    public async Task Only_the_record_folders_two_levels_down_are_read_and_only_yaml_files()
    {
        Write("engineering/csharp/good.yaml", Good);
        Write("engineering/csharp.inventory.yaml", Good.Replace("engineering.csharp.good", "engineering.csharp.inventory", StringComparison.Ordinal));
        Write("engineering/csharp/notes.md", "# notes\n");

        var listed = (await new FileRuleCatalog(_folder.FullName).List(Token)).Value;

        Assert.Equal(["engineering.csharp.good"], listed.Rules.Select(static r => r.Id));
        Assert.Equal(0, listed.Skipped);
    }

    [Fact]
    public async Task A_record_without_kind_section_status_or_checks_is_listed_with_empty_ones()
    {
        Write("engineering/csharp/bare.yaml", "id: engineering.csharp.bare\ntext: \"A bare rule text.\"\n");

        var rule = (await new FileRuleCatalog(_folder.FullName).List(Token)).Value.Rules.Single();

        Assert.Equal((string.Empty, string.Empty, string.Empty), (rule.Kind, rule.Section, rule.Status));
        Assert.Empty(rule.Checks);
    }

    [Fact]
    public async Task A_missing_rules_folder_lists_no_rule()
    {
        var listed = await new FileRuleCatalog(Path.Combine(_folder.FullName, "absent")).List(Token);

        Assert.True(listed.IsSuccess);
        Assert.Empty(listed.Value.Rules);
        Assert.Equal(0, listed.Value.Skipped);
    }

    [Fact]
    public void The_rules_folder_is_the_variable_then_the_checkout_then_the_toolkit()
    {
        var checkout = Directory.CreateDirectory(Path.Combine(_folder.FullName, "checkout", "skills", "_shared")).Parent!.Parent!.FullName;
        string? None(string name) => string.Equals(name, "HOME", StringComparison.Ordinal) ? "/home/x" : null;

        Assert.Equal("/set/rules", RuleFolders.Resolve(name => string.Equals(name, "AW_SINDRI_RULES_DIR", StringComparison.Ordinal) ? "/set/rules" : null, checkout));
        Assert.Equal(Path.Combine(checkout, "rules"), RuleFolders.Resolve(None, checkout));
        Assert.Equal(Path.Combine("/home/x", ".agentic-workflow", "toolkit", "rules"), RuleFolders.Resolve(None, _folder.FullName));
    }

    private void Write(string relative, string text)
    {
        var path = Path.Combine(_folder.FullName, relative);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}
