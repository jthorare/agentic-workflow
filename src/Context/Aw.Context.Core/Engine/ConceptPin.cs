namespace Aw.Context.Core.Engine;

/// <summary>A concept the owner pinned in the profile's 'index.concepts' (context concepts note §3): it takes its members first, and derived concepts fill the rest.</summary>
/// <param name="Id">The slug of its key ('scheduling' pins concept:scheduling), or null to derive one from the name.</param>
/// <param name="Name">What the map shows.</param>
/// <param name="Summary">A line about it, or null.</param>
/// <param name="Features">Feature node keys or key globs ('feature:shifts', 'feature:shifts.*') it holds.</param>
/// <param name="Paths">Repo-relative file globs; the features implemented there belong to it.</param>
/// <param name="DependsOn">Ids of the pins it declares it depends on (a Human 'depends-on' edge, spec 2026-10-27 §10.3).</param>
/// <param name="Owns">Names of the data stores it declares it owns, as the data-store extractor names them (a Human 'writes' edge).</param>
public sealed record ConceptPin(ConceptId? Id, string Name, string? Summary, IReadOnlyList<string> Features, IReadOnlyList<string> Paths, IReadOnlyList<string> DependsOn, IReadOnlyList<string> Owns)
{
    /// <summary>Other words the owner uses for it; a question using one anchors on the concept. Not part of the map hash (an alias edit rebuilds nothing).</summary>
    public IReadOnlyList<string> Aliases { get; init; } = [];

    /// <summary>A pin from the profile's plain values.</summary>
    /// <param name="id">The slug, or null.</param>
    /// <param name="name">The name; none becomes empty (the profile's rules refuse it before this).</param>
    /// <param name="summary">A line about it, or null.</param>
    /// <param name="features">Feature keys or key globs, or null for none.</param>
    /// <param name="paths">File globs, or null for none.</param>
    /// <returns>The pin.</returns>
    public static ConceptPin Of(ConceptId? id, string? name, string? summary, IEnumerable<string>? features, IEnumerable<string>? paths) =>
        new(id, name ?? string.Empty, summary, [.. features ?? []], [.. paths ?? []], [], []);

    /// <summary>The same pin with what the owner declared about its edges.</summary>
    /// <param name="dependsOn">Ids of the pins it depends on, or null for none.</param>
    /// <param name="owns">Names of the data stores it owns, or null for none.</param>
    /// <returns>The pin.</returns>
    public ConceptPin Declaring(IEnumerable<string>? dependsOn, IEnumerable<string>? owns) => this with { DependsOn = [.. dependsOn ?? []], Owns = [.. owns ?? []] };

    /// <summary>The same pin with the owner's other words for it.</summary>
    /// <param name="aliases">The aliases, or null for none.</param>
    /// <returns>The pin.</returns>
    public ConceptPin WithAliases(IEnumerable<string>? aliases) => this with { Aliases = [.. aliases ?? []] };
}
