namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>The PR a fix task is about: what the sheet shows and what the request carries.</summary>
/// <param name="Repo">owner/name.</param>
/// <param name="Number">The PR number.</param>
/// <param name="HeadSha">The full head sha.</param>
/// <param name="Sha7">The short head sha.</param>
/// <param name="Branch">The head branch.</param>
/// <param name="Author">The PR's author.</param>
/// <param name="Yours">True when the PR is the owner's own (Sindri posts the thread); false for a contributor's (item only).</param>
/// <param name="DocId">The claimed design doc's id; empty when none.</param>
internal sealed record ReviewFixTarget(string Repo, int Number, string HeadSha, string Sha7, string Branch, string Author, bool Yours, string DocId);
