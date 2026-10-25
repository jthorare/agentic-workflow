namespace Aw.Sindri.Features.CodeIndexJobs;

/// <summary>What an embed job did: counts and words only.</summary>
/// <param name="Repo">The repo.</param>
/// <param name="Outcome">ok (every symbol has a vector), pending (some remain) or later (a build held the repo's index, or it was not built yet: nothing changed, and the job is not a failure).</param>
/// <param name="Embedded">Symbols with a vector when the job ended.</param>
/// <param name="Embeddable">Symbols that should have one.</param>
/// <param name="Carried">Of the symbols with a vector, how many already had one when the job began (carried over from the previous index).</param>
/// <param name="Made">How many vectors this job made.</param>
/// <param name="Milliseconds">How long it ran.</param>
public sealed record EmbedCodeIndexResult(string Repo, string Outcome, int Embedded, int Embeddable, int Carried, int Made, long Milliseconds)
{
    /// <summary>Every symbol has a vector.</summary>
    public const string Ok = "ok";

    /// <summary>Some symbols still have none.</summary>
    public const string Pending = "pending";

    /// <summary>The job ended because a build held the repo's index or it was not built yet.</summary>
    public const string Later = "later";
}
