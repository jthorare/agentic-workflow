namespace Aw.Sindri.Ui.Testing;

/// <summary>The fix-task answers of review page spec §8.2 and the mockups (sheet-fix-o1..o3, -contrib, -half), spec-shaped until the API has goldens.</summary>
internal static class ReviewFixBodies
{
    /// <summary>A babysitter instance watching the PR took the thread.</summary>
    internal const string Babysitter = """{"item":"01J9Z3T7KQ","went":"babysitter","branch":"feat/retry-budget","thread":"https://github.com/acme/api/pull/2071#discussion_r1","session":null,"postCode":null,"postError":null}""";

    /// <summary>A live session on the head branch took it.</summary>
    internal const string Session = """{"item":"01J9Z3T7KQ","went":"session","branch":"feat/retry-budget","thread":"https://github.com/acme/api/pull/2071#discussion_r1","session":"claude, ~/repos/aw-wt/retry-budget","postCode":null,"postError":null}""";

    /// <summary>No session: the item waits in Work.</summary>
    internal const string Waiting = """{"item":"01J9Z3T7KQ","went":"waiting","branch":"feat/retry-budget","thread":"https://github.com/acme/api/pull/2071#discussion_r1","session":null,"postCode":null,"postError":null}""";

    /// <summary>A contributor's PR: the item only, no thread.</summary>
    internal const string ItemOnly = """{"item":"01J9Z3T7KQ","went":"waiting","branch":"feat/retry-budget","thread":null,"session":null,"postCode":null,"postError":null}""";

    /// <summary>The item was made and the thread wasn't posted (SND-REVIEW-006).</summary>
    internal const string HalfDone = """{"item":"01J9Z3T7KQ","went":"waiting","branch":"feat/retry-budget","thread":null,"session":null,"postCode":"SND-REVIEW-006","postError":"Item 01J9Z3T7KQ created in Work; posting to #2071 failed: GitHub answered 502 (bad gateway)."}""";
}
