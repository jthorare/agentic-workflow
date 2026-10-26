using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Stats;

namespace Aw.Sindri.Tests.Views;

/// <summary>
/// A stored view row is read back through the current record, so a record that gains a required property (or renames one) makes every row stored
/// under the old shape unreadable (#959). The fingerprint below covers the row records and everything they hold; when it changes, bump
/// <see cref="ViewSchema.Version"/> (the projector rebuilds the views from the ledger on the next start) and re-pin both values here.
/// </summary>
public sealed class ViewRowShapeTests
{
    private const int PinnedVersion = 13;
    private const string PinnedShape = "d08f112a867b1e60f63d379cfddbbb6f8682c247af13408b2e42d2e189851e5b";

    private static readonly Type[] _rows =
    [
        typeof(ServiceRow), typeof(ProfileRow), typeof(ItemRow), typeof(AliasRow), typeof(TimelineRow), typeof(RequestRow),
        typeof(HumanDecisionRow), typeof(NeedsYouRowRecord), typeof(JobRow), typeof(LaunchRow), typeof(CodeIndexRow), typeof(RecipeStatsRow),
    ];

    [Fact, Trait("Lane", "unit")]
    public void The_view_row_shapes_are_pinned_to_the_schema_version_so_a_changed_shape_rebuilds_the_views()
    {
        var shape = Fingerprint();

        Assert.True(
            string.Equals(PinnedShape, shape, StringComparison.Ordinal) && PinnedVersion == ViewSchema.Version,
            $"A view row record changed shape (or the version moved alone). Bump ViewSchema.Version, then set PinnedVersion to it and PinnedShape to \"{shape}\" in this test.");
    }

    private static string Fingerprint()
    {
        var lines = new SortedSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<Type>();
        foreach (var row in _rows)
        {
            Walk(row, lines, visited);
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', lines))));
    }

    private static void Walk(Type type, SortedSet<string> lines, HashSet<Type> visited)
    {
        var plain = Nullable.GetUnderlyingType(type) ?? type;
        foreach (var argument in plain.IsGenericType ? plain.GetGenericArguments() : [])
        {
            Walk(argument, lines, visited);
        }

        if (plain.IsEnum)
        {
            _ = lines.Add($"{plain.FullName}={string.Join('|', Enum.GetNames(plain))}");
        }
        else if (plain.Namespace?.StartsWith("Aw.Sindri", StringComparison.Ordinal) == true && visited.Add(plain))
        {
            var properties = plain.GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(static p => p.Name, StringComparer.Ordinal).ToList();
            _ = lines.Add($"{plain.FullName}({string.Join(',', properties.Select(static p => $"{p.Name}:{p.PropertyType}"))})");
            foreach (var property in properties)
            {
                Walk(property.PropertyType, lines, visited);
            }
        }
    }
}
