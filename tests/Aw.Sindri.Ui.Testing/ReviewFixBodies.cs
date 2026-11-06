namespace Aw.Sindri.Ui.Testing;

/// <summary>The fix-task answers of review page spec §8.2 and the mockups (sheet-fix-o1..o3, -contrib, -half), spec-shaped until the API has goldens.</summary>
internal static class ReviewFixBodies
{
    /// <summary>A babysitter instance watching the PR took the thread.</summary>
    internal const string Babysitter = """{"itemId":"01J9Z3T7KQ","posted":true,"postCode":null,"postError":null,"went":"babysitter","session":null}""";

    /// <summary>A live session on the head branch took it.</summary>
    internal const string Session = """{"itemId":"01J9Z3T7KQ","posted":true,"postCode":null,"postError":null,"went":"session","session":"claude, ~/repos/aw-wt/retry-budget"}""";

    /// <summary>No session: the item waits in Work.</summary>
    internal const string Waiting = """{"itemId":"01J9Z3T7KQ","posted":true,"postCode":null,"postError":null,"went":"waiting","session":null}""";

    /// <summary>A contributor's PR: the item only, no thread.</summary>
    internal const string ItemOnly = """{"itemId":"01J9Z3T7KQ","posted":false,"postCode":null,"postError":null,"went":"waiting","session":null}""";

    /// <summary>The item was made and the thread wasn't posted (SND-REVIEW-006).</summary>
    internal const string HalfDone = """{"itemId":"01J9Z3T7KQ","posted":false,"postCode":"SND-REVIEW-006","postError":"Item 01J9Z3T7KQ created in Work; posting to #2071 failed: GitHub answered 502 (bad gateway).","went":"waiting","session":null}""";
}
