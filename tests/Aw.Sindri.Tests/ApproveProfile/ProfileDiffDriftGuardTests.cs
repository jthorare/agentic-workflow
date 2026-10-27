using System.Reflection;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.ApproveProfile;

/// <summary>
/// The drift guard for issue 1147: the profile diff is a hand-written list of paths (ProfileDiff.Flatten), and the hash covers every property of these records. A property added to one of
/// them fails here until it is pinned below, and pinning it means giving it a path in <see cref="ProfileDiff"/> (and a case in <see cref="DiffCoversHashedKeysCheckTests"/>).
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ProfileDiffDriftGuardTests
{
    private static readonly (Type Type, string[] Properties)[] _pinned =
    [
        (typeof(SindriProfile), ["SchemaVersion", "User", "Tenant", "Repos", "Services", "Api", "Workflow", "Context", "Index", "Launch", "Agents", "Setup", "Sandbox"]),
        (typeof(RepoEntry), ["Name", "Path", "Index", "Workflow"]),
        (typeof(RepoWorkflowSettings), ["Judge"]),
        (typeof(IndexSettings), ["DenyPaths", "UtilityGlobs", "MaxFileKB", "MaxTotalMB", "Embeddings", "Graph", "Roles", "Concepts", "Ignore", "Demote"]),
        (typeof(EmbeddingSettings), ["Enabled", "Url", "Model"]),
        (typeof(GraphSettings), ["Enabled", "MaxMB"]),
        (typeof(ConceptSettings), ["Id", "Name", "Summary", "Features", "Paths"]),
        (typeof(SetupSettings), ["Mcp"]),
        (typeof(ApiSettings), ["Port"]),
        (typeof(ServiceSettings), ["Every"]),
        (typeof(LaunchSettings), ["Default", "Repos", "ConfirmWindow"]),
        (typeof(LaunchRepoSettings), ["Default"]),
        (typeof(AgentSettings), ["SoftLimit", "Targets"]),
        (typeof(AgentLimits), ["DailyBudget", "StepArgvHash"]),
        (typeof(WorkflowSettings), ["DeskTimeout", "EventHold", "Effects", "DispatchDelay", "Judge", "Agent", "Forge", "Route", "Packs", "Line", "Plan", "Prove", "Build", "Ship", "Design", "Review", "Steps", "Docs", "Pr", "Scoping"]),
        (typeof(ScopingSettings), ["HistoryWindow"]),
        (typeof(HistoryWindowSettings), ["Since", "Before"]),
    ];

    [Fact]
    public void Every_property_of_the_hashed_records_is_one_the_diff_was_written_for()
    {
        foreach (var (type, pinned) in _pinned)
        {
            var actual = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(static p => p.Name).Order(StringComparer.Ordinal).ToList();
            var expected = pinned.Order(StringComparer.Ordinal).ToList();

            Assert.True(
                actual.SequenceEqual(expected, StringComparer.Ordinal),
                $"{type.Name} now has [{string.Join(", ", actual)}] but the diff was written for [{string.Join(", ", expected)}]: give the new property a path in ProfileDiff.Flatten, add it to DiffCoversHashedKeysCheckTests, then pin it here");
        }
    }
}
