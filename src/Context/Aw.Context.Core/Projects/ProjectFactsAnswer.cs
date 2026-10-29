using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Projects;

/// <summary>What the record store knows about projects, read from stored data only (no Linear call).</summary>
/// <param name="Owner">Who the Linear key belongs to, or null while the source hasn't said.</param>
/// <param name="Projects">The attached projects the store holds and the caller may see, in the request's order.</param>
/// <param name="Catalog">Every stored project the caller may see, by title: the page's 'From Linear' tab.</param>
/// <param name="Teams">The Linear team keys the profile reads ('context.sources.linear.teams').</param>
/// <param name="FirstReadDone">The projects feed has finished its first read (its phase moved past Recent).</param>
public sealed record ProjectFactsAnswer(UserRef? Owner, IReadOnlyList<ProjectFact> Projects, IReadOnlyList<ProjectFact> Catalog, IReadOnlyList<string> Teams, bool FirstReadDone);
