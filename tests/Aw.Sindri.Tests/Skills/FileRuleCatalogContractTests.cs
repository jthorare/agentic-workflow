using System.Globalization;
using System.Text;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Infrastructure.Skills;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Skills;

/// <summary>Binds <see cref="FileRuleCatalog"/> to <see cref="RuleCatalogPortContract"/> over a temp rules folder: one 'engineering/csharp/&lt;slug&gt;.yaml' per seed.</summary>
[Lane(TestLane.Integration)]
public sealed class FileRuleCatalogContractTests : RuleCatalogPortContract, IDisposable
{
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("aw-rule-catalog-");

    public void Dispose() => _folder.Delete(recursive: true);

    protected override async ValueTask<IRuleCatalogPort> Create(IReadOnlyList<RuleCatalogSeed> rules)
    {
        var folder = Directory.CreateDirectory(Path.Combine(_folder.FullName, "engineering", "csharp"));
        foreach (var rule in rules)
        {
            var yaml = new StringBuilder();
            yaml.Append(CultureInfo.InvariantCulture, $"schemaVersion: 1\nid: {rule.Id}\nkind: engineering\nsection: \"{rule.Section}\"\ntext: \"{rule.Text}\"\nwhy: \"Because it is decided.\"\n");
            yaml.Append(rule.Checks.Count == 0 ? "checks: []\n" : "checks:\n");
            foreach (var check in rule.Checks)
            {
                yaml.Append(CultureInfo.InvariantCulture, $"  - type: {check.Type}\n    id: \"{check.Id}\"\n    status: active\n");
            }

            yaml.Append("gap: none\nstatus: active\n");
            await File.WriteAllTextAsync(Path.Combine(folder.FullName, rule.Id + ".yaml"), yaml.ToString(), TestContext.Current.CancellationToken);
        }

        return new FileRuleCatalog(_folder.FullName);
    }
}
