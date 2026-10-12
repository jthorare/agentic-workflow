using ArchUnitNET.Domain;
using ArchUnitNET.Domain.Dependencies;

namespace Aw.Architecture.Tests.Rules;

/// <summary>One type-to-type dependency with both ends placed in the package shape.</summary>
/// <param name="Origin">The depending type.</param>
/// <param name="From">The origin's place, or null outside a shaped package.</param>
/// <param name="Target">The type depended on.</param>
/// <param name="To">The target's place, or null outside a shaped package.</param>
/// <param name="TargetMembers">The target's members the origin calls (ArchUnitNET method names, e.g. GetEnvironmentVariable(System.String)).</param>
/// <param name="TargetFields">The target's fields the origin reads or writes (ArchUnitNET field names).</param>
public sealed record Edge(IType Origin, Place? From, IType Target, Place? To, IReadOnlyList<string> TargetMembers, IReadOnlyList<string> TargetFields)
{
    /// <summary>Every dependency of the given types, one per top-level origin and target, with the members called.</summary>
    /// <param name="types">The types whose dependencies to list.</param>
    /// <param name="scope">The shaped packages in scope.</param>
    /// <returns>The edges.</returns>
    public static IReadOnlyList<Edge> All(IEnumerable<IType> types, Scope scope) =>
        [.. types
            .SelectMany(t => t.Dependencies.Select(d => (Origin: t, Dependency: d)))
            .Where(p => !ReferenceEquals(p.Origin, p.Dependency.Target) && p.Dependency.Target is not GenericParameter)
            .GroupBy(p => (TypeNames.Qualified(p.Origin), p.Dependency.Target.FullName))
            .Select(g => new Edge(
                g.First().Origin,
                Place.Of(g.First().Origin, scope),
                g.First().Dependency.Target,
                Place.Of(g.First().Dependency.Target, scope),
                [.. g.Select(p => p.Dependency).OfType<MethodCallDependency>().Select(m => m.TargetMember.Name).Distinct(StringComparer.Ordinal)],
                [.. g.Select(p => p.Dependency).OfType<AccessFieldDependency>().Select(f => f.TargetMember.Name).Distinct(StringComparer.Ordinal)]))];

    /// <summary>"Origin -> Target" for messages.</summary>
    public string Text => $"{TypeNames.Qualified(Origin)} -> {Target.FullName}";
}
