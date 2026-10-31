using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Pages and actions section (pages spec §3.4, §3.5, §5).</summary>
internal static partial class Labels
{
    /// <summary>The section's head.</summary>
    internal const string ContextPagesHead = "Pages and actions";

    /// <summary>The section's head over the pages in no concept.</summary>
    internal const string ContextPagesNoConceptHead = "Pages in no concept";

    /// <summary>The mark of a page the map found and nobody named yet.</summary>
    internal const string ContextPagesSuggested = "Suggested";

    /// <summary>The mark of a page some code implements.</summary>
    internal const string ContextPagesCode = "Code found";

    /// <summary>The mark of a page no code implements yet.</summary>
    internal const string ContextPagesNoCode = "No code found yet";

    /// <summary>The section while its read runs.</summary>
    internal const string ContextPagesLoading = "Loading pages…";

    /// <summary>A concept's section when the read failed.</summary>
    internal const string ContextPagesFailed = "Couldn't read this concept's pages just now. Try again.";

    /// <summary>The pages in no concept when the read failed.</summary>
    internal const string ContextPagesNoConceptFailed = "Couldn't read the pages just now. Try again.";

    /// <summary>The button that reads the section again.</summary>
    internal const string ContextPagesRetry = "Try again";

    /// <summary>A concept with no pages.</summary>
    internal const string ContextPagesEmpty = "No pages or actions found for this concept yet. Name one with New page.";

    /// <summary>No page is in no concept.</summary>
    internal const string ContextPagesNoConceptEmpty = "Every page relates to a concept.";

    /// <summary>The close button of the pages in no concept.</summary>
    internal const string ContextPagesClose = "Close pages in no concept";

    /// <summary>The Context page's head link: "Pages in no concept (12)".</summary>
    /// <param name="count">How many pages relate to no concept.</param>
    /// <returns>The link's words.</returns>
    internal static string ContextPagesNoConceptLink(int count) => string.Create(CultureInfo.InvariantCulture, $"Pages in no concept ({count})");

    /// <summary>The sentence when a profile write's job (a page or a pin) was still running after the app stopped waiting for it.</summary>
    internal static string ProfileWriteTimedOut => "The write is still running. Check Jobs, then try again.";

    /// <summary>The line under a page that names the map's own page: "From the map: GET /schedule/week".</summary>
    /// <param name="surface">The map's page, in words.</param>
    /// <returns>The line.</returns>
    internal static string ContextPagesFromMap(string surface) => "From the map: " + surface;

    /// <summary>The button past the cap: "Show all 34".</summary>
    /// <param name="total">How many pages there are.</param>
    /// <returns>The button's words.</returns>
    internal static string ContextPagesShowAll(int total) => string.Create(CultureInfo.InvariantCulture, $"Show all {total}");

    /// <summary>An action row's page: "on Schedule week".</summary>
    /// <param name="page">The page's name.</param>
    /// <returns>The words.</returns>
    internal static string ContextPagesOn(string page) => "on " + page;

    /// <summary>An action row's other concepts: "also in Approval workflow".</summary>
    /// <param name="concepts">The other concepts' names.</param>
    /// <returns>The words.</returns>
    internal static string ContextPagesAlsoIn(IEnumerable<string> concepts) => "also in " + string.Join(", ", concepts);

    /// <summary>An action row's decision count: "1 decision", "2 decisions".</summary>
    /// <param name="count">How many decisions decide it.</param>
    /// <returns>The words.</returns>
    internal static string ContextPagesDecisions(int count) => string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? "decision" : "decisions")}");

    /// <summary>An action row's code: "code: SubmitButton.tsx".</summary>
    /// <param name="file">The file's name.</param>
    /// <returns>The words.</returns>
    internal static string ContextPagesCodeFile(string file) => "code: " + file;

    /// <summary>A page row as a screen reader reads it: its name and marks.</summary>
    /// <param name="name">The page's name.</param>
    /// <param name="marks">Its marks.</param>
    /// <returns>The words.</returns>
    internal static string ContextPagesRowName(string name, IEnumerable<string> marks) => name + ", " + string.Join(", ", marks);
}
