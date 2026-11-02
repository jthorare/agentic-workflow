namespace Aw.Sindri.Core.Projects.Facts;

/// <summary>One read of the stored Linear projects (projects page spec §6.1).</summary>
/// <param name="Owner">Who Linear says the owner is (the key's viewer), or null until the sync has read it.</param>
/// <param name="Projects">The projects the owner leads, then the attached ones the store holds, in the order asked.</param>
/// <param name="Catalog">Every stored project in scope, sorted by title: the From Linear tab, a url's lookup and the led check.</param>
/// <param name="Teams">The synced Linear team keys ('context.sources.linear.teams'), for SND-PROJECT-003's text.</param>
/// <param name="FirstReadDone">Whether the 'linear-projects' feed has finished its first read (page state 5 until then).</param>
public sealed record ProjectFactsSnapshot(
    ProjectPerson? Owner,
    IReadOnlyList<LinearProjectFact> Projects,
    IReadOnlyList<LinearProjectFact> Catalog,
    IReadOnlyList<string> Teams,
    bool FirstReadDone);
